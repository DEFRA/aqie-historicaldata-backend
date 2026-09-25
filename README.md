# aqie-historicaldata-backend

Backend service for the **historical data** and **data selection** features of the Air Quality
Information for England (AQIE) service. It fetches historical air quality measurements from
Defra's UK-AIR ATOM feeds and related APIs, aggregates them, exports them as CSV, and delivers
them to users via presigned S3 URLs — either immediately or asynchronously by email.

This is the only .NET service in the AQIE estate (see [Why .NET?](#why-net)).

- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Local development](#local-development)
- [Environment variables](#environment-variables)
- [API endpoints](#api-endpoints)
- [Observations API](#observations-api)
- [Background services](#background-services)
- [MongoDB collections](#mongodb-collections)
- [Why .NET?](#why-net)
- [Licence](#licence)

---

## How it works

AQIE lets the public download historical air quality data — for a single monitoring station, or
for a filtered selection across many stations. The pipeline for downloads is the same in both
cases: **fetch** observations from the ATOM feeds, **aggregate** them to hourly/daily/annual,
**export** to CSV, then **upload** to S3 and hand back a presigned URL.

There are three journeys:

- **Single station download** (`AtomHistoryHourlydata`) — one station, one year, returned
  synchronously as a presigned URL.
- **Data selection** (`AtomDataSelection`, `AtomEmailJobDataSelection`) — many stations × many
  years. Too slow to hold an HTTP connection open, so it writes a `Pending` job to MongoDB and a
  background service processes it and emails the link. Poll with `AtomDataSelectionJobStatus`,
  then fetch the URL with `AtomDataSelectionPresignedUrlMail`.
- **Observations API** (`AtomHistoryObservations`, `AtomObservationStations`) — one station over
  a chosen time window, returned inline as JSON for a front end to render. No CSV, no S3. See
  [Observations API](#observations-api).

Stations can be filtered by UK country or local authority. Country boundaries ship as GeoJSON in
[GeoBoundaries](AqieHistoricaldataBackend/GeoBoundaries); `AtomDataSelectionStationBoundryService`
does point-in-polygon tests with NetTopologySuite and converts between OSGB36 British National
Grid and WGS84 with ProjNET, since the upstream sources don't use a consistent coordinate system.

### Data sources

Two networks are supported and behave differently. **AURN** (Automatic Urban and Rural Network)
takes its station metadata live from the Ricardo API. **Non-AURN** metadata is seeded into MongoDB
from Excel files in S3 at startup.

| Upstream | Base URL | Used for |
| --- | --- | --- |
| UK-AIR ATOM feeds | `https://uk-air.defra.gov.uk/` | Hourly observations, one feed per station per year, under `data/atom-dls/observations/auto/` (automatic) or `.../non-auto/` |
| Ricardo UK-AIR API | `https://api-ukair.defra.gov.uk/` | AURN station metadata. Requires credentials |
| LAQM Portal | `https://www.laqmportal.co.uk/` | Local authority list and non-AURN station data. Requires an API key |
| aqie-notify-service | `https://aqie-notify-service.{Environment}.cdp-int.defra.cloud/` | "Your download is ready" emails |
| AWS S3 | — | CSV output and the non-AURN master spreadsheets |

---

## Requirements

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — recommended, as Compose
  starts MongoDB and LocalStack alongside the app

---

## Local development

### Docker Compose

Starts LocalStack (S3), Redis, MongoDB and this service:

```bash
docker compose up --build -d
```

The service listens on **http://localhost:8080**. LocalStack is on `4566` (initialised by
[compose/start-localstack.sh](compose/start-localstack.sh) with the dummy credentials in
[compose/aws.env](compose/aws.env)), MongoDB on `27019`, Redis on `6381`.

The MongoDB and Redis host ports are deliberately offset from their defaults so the stack can run
alongside the other AQIE services (aqie-back-end uses `27017`/`6379`, aqie-forecast-api uses
`27018`/`6380`). Inside the Compose network the containers still use the standard ports.

A more extensive local platform is available at
[DEFRA/cdp-local-environment](https://github.com/DEFRA/cdp-local-environment).

### .NET CLI

Listens on **http://localhost:5000**. Note the launch profile is named after the project, not
`Development`:

```bash
docker compose up -d mongodb
dotnet run --project AqieHistoricaldataBackend --launch-profile AqieHistoricaldataBackend
```

`appsettings.Development.json` points at `mongodb://127.0.0.1:27017`, so if you are using the
Compose MongoDB you need to override the port:

```bash
Mongo__DatabaseUri=mongodb://127.0.0.1:27019 \
  dotnet run --project AqieHistoricaldataBackend --launch-profile AqieHistoricaldataBackend
```

### Tests

```bash
dotnet test
```

xUnit, with Moq / NSubstitute / FluentAssertions and `RichardSzalay.MockHttp` for stubbing HTTP.
Tests also run in the Docker build, so a failing test fails the image build.

### Inspecting MongoDB

```bash
docker compose exec mongodb mongosh
use aqie-historicaldata-backend
db.aqie_csvemailexport_jobs.find().sort({ _id: -1 }).limit(5).pretty()
```

In deployed environments, use the CDP Terminal.

---

## Environment variables

There is no `.env` in this repo. Config comes from `appsettings.json` /
`appsettings.Development.json` plus environment variables, injected by CDP when deployed. Locally,
export them or add them to the `your-backend` `environment:` block in [compose.yml](compose.yml).

| Variable | Required | Purpose |
| --- | --- | --- |
| `RICARDO_API_KEY` | For AURN | Ricardo UK-AIR API login. Despite the name this is an email address, not a key |
| `RICARDO_API_VALUE` | For AURN | Ricardo UK-AIR API password |
| `LAQM_API_KEY` | For LA filtering | `X-API-Key` header for the LAQM Portal |
| `LAQM_USERID` | For LA filtering | `X-API-PartnerId` header for the LAQM Portal |

On CDP these are set as service secrets in the portal. Locally, put them in
`compose/secrets.env`, which is git-ignored and loaded automatically if present:

```bash
cat > compose/secrets.env <<'EOF'
RICARDO_API_KEY=your.email@example.com
RICARDO_API_VALUE=your-password
LAQM_API_KEY=
LAQM_USERID=
EOF
docker compose up -d --force-recreate your-backend
```

Do not put credentials in `compose/aws.env` — that file is committed.
| `S3_BUCKET_NAME` | Yes | Bucket for CSV exports and master spreadsheets. Startup seeding throws if unset |
| `POLLUTANT_MASTER_KEY` | For non-AURN | S3 key of the non-AURN pollutant master `.xlsx` |
| `POLLUTANT_STATION_MASTER_KEY` | For non-AURN | S3 key of the non-AURN station details `.xlsx` |
| `NOTIFY_BASEADDRESS` | For email jobs | Base address of the notify service |
| `NOTIFY_URL` | For email jobs | Path on the notify service used to send the email |
| `EMAIL_TEMPLATEID` | For email jobs | GOV.UK Notify template ID |
| `EMAIL_BASEADDRESS` | For email jobs | Prefix for the download link embedded in the email |

Optional / platform:

| Variable | Default | Purpose |
| --- | --- | --- |
| `TIME_INTERVAL` | `45` | Minutes between email job processing runs |
| `Environment` | `dev` | Used to build the notify service hostname |
| `HTTP_PROXY` | — | Outbound proxy. Credentials in the URI are supported and stripped before use |
| `TRUSTSTORE_*` | — | Any var prefixed `TRUSTSTORE` is treated as a base64 CA cert and loaded at startup |
| `SERVICE_VERSION` | `""` | Stamped onto log entries |
| `ASPNETCORE_ENVIRONMENT` | — | `Development` selects `appsettings.Development.json` |
| `Mongo__DatabaseUri` | see appsettings | Connection string. Set automatically in CDP, using IAM auth |
| `Mongo__DatabaseName` | `aqie-historicaldata-backend` | Database name |

---

## API endpoints

All routes are registered at the root (no `/api` prefix) in
[AtomHistoryEndpoints.cs](AqieHistoricaldataBackend/Atomfeed/Endpoints/AtomHistoryEndpoints.cs).

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/health` | Health check |
| GET | `/AtomHistoryObservations` | Single-station observations as JSON over a time window. See [Observations API](#observations-api) |
| GET | `/AtomObservationStations` | Station list keyed by the id the observations endpoint expects |
| GET, POST | `/AtomHistoryHourlydata` | Single-station download. Returns a presigned S3 URL |
| POST | `/AtomHistoryexceedence` | Exceedence data for a station (readings above statutory thresholds) |
| POST | `/AtomDataSelection` | Runs a multi-station data selection synchronously |
| POST | `/AtomEmailJobDataSelection` | Queues an asynchronous data selection job. Returns `"Success"` / `"Failure"` |
| POST | `/AtomDataSelectionJobStatus` | Job status for a given `jobId` |
| POST | `/AtomDataSelectionPresignedUrlMail` | Presigned URL for a completed job; flags the mail as sent |
| POST | `/AtomDataSelectionNonAurnNetworks` | Non-AURN network station and pollutant data |
| GET | `/AtomDataSelectionPollutantMaster` | All pollutants from the pollutant master |
| POST | `/AtomDataSelectionPollutantDataSource` | Pollutant details for a given data source |

Every POST takes the same loosely-typed `QueryStringData` object
([AtomHistoryModel.cs](AqieHistoricaldataBackend/Atomfeed/Models/AtomHistoryModel.cs)) — all fields
are optional strings and which ones matter depends on the endpoint. The main ones are `SiteId`,
`Year`, `DownloadPollutant`, `DownloadPollutantType` (`Hourly`/`Daily`/`Annual`), `dataSource`
(`AURN`/`NON-AURN`), `Region` + `regiontype` for boundary filtering, and `jobId` / `email` for the
asynchronous journey.

```bash
curl http://localhost:8080/health

# Single station download — returns a presigned S3 URL
curl -X POST http://localhost:8080/AtomHistoryHourlydata \
  -H 'Content-Type: application/json' \
  -d '{ "SiteId": "CLL2", "SiteName": "London Bloomsbury", "Year": "2019",
        "DownloadPollutant": "Nitrogen dioxide", "DownloadPollutantType": "Hourly" }'

# Queue an email job, then poll it
curl -X POST http://localhost:8080/AtomEmailJobDataSelection \
  -H 'Content-Type: application/json' \
  -d '{ "Region": "England", "regiontype": "country", "Year": "2023",
        "pollutantName": "NO2", "dataSource": "AURN", "email": "you@example.com" }'

curl -X POST http://localhost:8080/AtomDataSelectionJobStatus \
  -H 'Content-Type: application/json' -d '{ "jobId": "<jobId>" }'
```

Note that the handlers catch all exceptions and return `404 Not Found`, so a 404 usually means
something went wrong upstream rather than that no data exists — check the logs.
The observations endpoints are the exception — they distinguish their status codes properly.

---

## Observations API

Two endpoints serve the front end directly as JSON. They reuse the same fetch-and-parse chain as
the download endpoints but stop before the CSV step — nothing is written to S3 and nothing is
persisted.

### `GET /AtomHistoryObservations`

A single station's readings over a time window.

| Parameter | Required | Default | Values |
| --- | --- | --- | --- |
| `siteId` | yes | — | Station code, e.g. `CLL2`. Same id as `/AtomObservationStations` returns |
| `period` | no | `7days` | `24hours`, `7days`, `30days`, `year` |
| `aggregation` | no | `hourly` | `hourly`, `daily` |
| `pollutant` | no | all | Display name or code: `Nitrogen dioxide`/`NO2`, `PM10`, `PM2.5`/`PM25`, `Ozone`/`O3`, `Sulphur dioxide`/`SO2` |
| `network` | no | `AURN` | `AURN`, `NON-AURN`. Selects the `auto/` or `non-auto/` feed |
| `anchor` | no | `latest` | `latest` ends the window at the newest reading; `now` ends it at the current time |
| `year` | no | current | Anchor year, 1960 to present |

Unlike `DownloadPollutant` on the CSV endpoints, an unrecognised `pollutant` is rejected with a
`400` rather than silently falling back to all five.

```bash
curl 'http://localhost:8080/AtomHistoryObservations?siteId=CLL2&period=24hours&pollutant=NO2&year=2019'
```

```json
{
  "siteId": "CLL2",
  "network": "AURN",
  "period": "24hours",
  "aggregation": "hourly",
  "anchor": "latest",
  "windowFrom": "2019-12-30T23:00:00Z",
  "windowTo": "2019-12-31T23:00:00Z",
  "from": "2019-12-31T00:00:00Z",
  "to": "2019-12-31T23:00:00Z",
  "pollutants": ["Nitrogen dioxide"],
  "count": 24,
  "observations": [
    {
      "timestamp": "2019-12-31T00:00:00Z",
      "pollutant": "Nitrogen dioxide",
      "pollutantCode": "NO2",
      "value": 44.52505,
      "status": "V"
    }
  ]
}
```

`windowFrom`/`windowTo` are the bounds that were *requested*; `from`/`to` are the bounds of the
data actually returned. A gap between them means the feed has no readings for part of the window.

With `aggregation=daily`, each entry covers one day and carries a `capture` ratio instead of a
`status`:

```json
{ "timestamp": "2019-12-25", "pollutant": "PM10", "pollutantCode": "PM10", "value": 10.52, "capture": 1 }
```

### `GET /AtomObservationStations`

The station list, keyed by the same `siteId` the observations endpoint expects, so the two cannot
drift apart. AURN metadata comes from the Ricardo API; non-AURN from MongoDB.

| Parameter | Required | Default | Values |
| --- | --- | --- | --- |
| `network` | no | `AURN` | `AURN`, `NON-AURN` |
| `pollutant` | no | all | Filters to stations that measure it. Name or code |

```json
{
  "network": "AURN",
  "count": 173,
  "stations": [
    {
      "siteId": "CLL2",
      "name": "London Bloomsbury",
      "network": "AURN",
      "latitude": 51.52229,
      "longitude": -0.125889,
      "region": "Greater London",
      "areaType": "Urban",
      "siteType": "Background",
      "pollutants": [
        { "name": "Nitrogen dioxide", "code": "NO2", "startDate": "01/01/1992" }
      ]
    }
  ]
}
```

`siteId` is Ricardo's `localSiteId` — the form the ATOM feed filenames use (`CLL2`), **not** the
`UKA00315` form that `aqie-back-end` `/monitoringStations` publishes as `localSiteID`.

Pollutant names are normalised. Ricardo HTML-encodes subscripts and splits PM into instrument
variants (`PM<sub>10</sub> particulate matter (Hourly measured)`, `Volatile PM<sub>10</sub> ...`);
these collapse onto a single `PM10` entry, matching the download journey's mapping. Pollutants
outside the served five are passed through with markup stripped and an empty `code`.

**Closed stations are included.** The upstream call uses `with-closed=true`, so the list contains
sites that stopped reporting years ago — useful for historical data, but check each pollutant's
`endDate` before implying a station is live.

Requires `RICARDO_API_KEY` and `RICARDO_API_VALUE` for the AURN path; without them the endpoint
returns `502`.

### Behaviour worth knowing

- **`value` is `null`, never `-99`.** The upstream no-data sentinel is normalised away. For daily
  aggregation, `value` is also `null` when `capture` falls below 0.75, so a poorly-covered day is
  distinguishable from a genuine reading of zero.
- **`pollutantCode` is the stable identifier.** Switch on it rather than the display name.
- **Windows anchor on the latest reading by default, not wall-clock now.** The Defra feeds publish
  with a lag, so `anchor=latest` avoids routinely returning nothing. Pass `anchor=now` when you
  need a true trailing window — for example to report honest data capture.
- **Cross-year windows fetch twice.** The ATOM feed is one file per station per year, so a window
  spanning 1 January pulls the preceding year's feed and stitches the two together.
- **An unpublished current year falls back one year**, but only when `year` was not given
  explicitly.
- **`period=year` with `aggregation=hourly` and no pollutant is rejected** with a `400`. That
  combination is ~43,800 objects.
- **Status codes are meaningful**: `400` for bad parameters (reason in `error`), `404` when the
  site and period genuinely yield nothing, `502` when the upstream Defra feed could not be read,
  `500` on an unexpected failure. A `404` and a `502` are deliberately distinct — an empty station
  and a broken feed are different problems.

### Publication lag

Measured 2026-09-25: the ATOM feeds are regenerated daily at roughly **00:30–00:40 UTC** and
contain readings up to **23:00 the previous day**. Observed lag was 15.3 hours at 14:19 UTC for
both `CLL2` and `MY1`, so in practice it ranges from about 1.5 hours just after regeneration to
about 25 hours just before the next.

**This is not a near-real-time source.** If you need current-hour data, use a different upstream.

### Caching

Parsed feed rows are cached in memory for 15 minutes per `(network, siteId, year, pollutant)`, so
the two calls a station page typically makes only parse the year's XML once. Station lists are
cached for 6 hours. The cache is per-instance — there is no shared Redis cache, so with multiple
replicas each warms independently.

---

## Background services

| Service | Trigger | What it does |
| --- | --- | --- |
| [AtomDataSelectionEmailJobHostedService](AqieHistoricaldataBackend/Atomfeed/Services/AtomDataSelectionEmailJobHostedService.cs) | Every `TIME_INTERVAL` minutes (default 45) | Polls `aqie_csvemailexport_jobs` for `Pending` jobs. Runs the selection, builds the CSV, uploads to S3, and asks aqie-notify-service to email the presigned URL. Moves the job Pending → Processing → Completed/Failed. There is no retry — a failure at any step marks the job `Failed` |
| [AtomNonAurnNetworksSeedHostedService](AqieHistoricaldataBackend/Atomfeed/Services/AtomNonAurnNetworksSeedHostedService.cs) | Once, at startup | Downloads the non-AURN pollutant and station spreadsheets from S3 and replaces the corresponding MongoDB collections. Uses a Mongo-based distributed lock (`aqie_atom_seed_locks`, 10-minute TTL) so only one instance seeds when several start together |

To exercise the email pipeline without waiting 45 minutes, set `TIME_INTERVAL=1` and restart.

---

## MongoDB collections

Database: `aqie-historicaldata-backend`

| Collection | Contents |
| --- | --- |
| `aqie_csvexport_jobs` | Data selection export jobs |
| `aqie_csvemailexport_jobs` | Asynchronous email delivery jobs, with status and presigned URL |
| `aqie_atom_seed_locks` | Distributed startup-seeding lock (TTL indexed) |
| `aqie_atom_non_aurn_networks_pollutant_master` | Non-AURN pollutant lookup, seeded from S3 Excel |
| `aqie_atom_non_aurn_networks_station_details` | Non-AURN station metadata, seeded from S3 Excel |

---

## Why .NET?

Every other AQIE service is Node.js. Per Karthick Muthukrishnan (architect), both were evaluated
and .NET was chosen for the high data volume and performance requirements — specifically its
ability to process ATOM feed data in parallel efficiently.

That shows up in the code: `AtomDataSelectionHourlyFetchService` fans out across every
(station × year) pair with `Parallel.ForEachAsync`, and `AtomDataSelectionStationBoundryService`
runs partitioned parallel point-in-polygon matching over all stations.

---

## Licence

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

<http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3>

The following attribution statement MUST be cited in your products and applications when using
this information.

> Contains public sector information licensed under the Open Government Licence v3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
