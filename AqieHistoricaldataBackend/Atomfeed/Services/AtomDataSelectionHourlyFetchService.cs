using Hangfire;
using System.Collections.Concurrent;
using System.Diagnostics;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionHourlyFetchService(
        ILogger<HistoryexceedenceService> logger,
        IHttpClientFactory httpClientFactory)
        : AtomFeedFetchServiceBase(httpClientFactory), IAtomDataSelectionHourlyFetchService
    {
        protected override ILogger Logger => logger;

        public async Task<List<FinalData>> GetAtomDataSelectionHourlyFetchService(
            List<SiteInfo> filteredstationpollutant, string pollutantName, string filteryear, QueryStringData data)
        {
            try
            {
                var years = filteryear.Split(',');
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation("Fetch and processing started in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);

                var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                var resultsBag = new ConcurrentBag<FinalData>();

                var siteYearPairs = filteredstationpollutant
                    .SelectMany(siteinfo => years.Select(year => new { siteinfo, year }));

                await Parallel.ForEachAsync(siteYearPairs, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async (pair, ct) =>
                {
                    try
                    {
                        var atomJsonCollection = await FetchAtomFeedAsync(pair.siteinfo.LocalSiteId, pair.year, data.dataSource);
                        var result = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, pair.siteinfo);
                        foreach (var item in result)
                            resultsBag.Add(item);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing site {SiteID} for year {Year}: {Error}", pair.siteinfo.LocalSiteId, pair.year, ex.Message);
                        await File.AppendAllTextAsync("error_log.txt", $"{DateTime.Now}: Error processing site {pair.siteinfo.LocalSiteId} for year {pair.year}: {ex.Message}{Environment.NewLine}", ct);
                    }
                });

                stopwatch.Stop();
                Logger.LogInformation("Fetch and processing completed in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);
                await File.AppendAllTextAsync("fetch_duration_log.txt",
                    $"Fetch completed at {DateTime.Now} - Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}{Environment.NewLine}");

                if(data.dataSource == "AURN" && data.Days == "7days")
                {
                    var last7DaysResults = AtomDataSelectionFilterLast7Days.ByStartTime(resultsBag);
                    return last7DaysResults;
                }
                else 
                { 
                    return resultsBag.ToList(); 
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GetAtomDataSelectionHourlyFetchService");
                return new List<FinalData>();
            }
        }

        private static List<PollutantDetails> GetPollutantsToDisplay(string? filter)
        {
            var allPollutants = new List<PollutantDetails>
            {
                new() { PollutantName = "Nitrogen dioxide",                         PollutantMasterUrl = "8"      },
                new() { PollutantName = "Particulate matter (PM10)",                PollutantMasterUrl = "5"      },
                new() { PollutantName = "Fine particulate matter (PM2.5)",          PollutantMasterUrl = "6001"   },
                new() { PollutantName = "Ozone",                                    PollutantMasterUrl = "7"      },
                new() { PollutantName = "Sulphur dioxide",                          PollutantMasterUrl = "1"      },
                new() { PollutantName = "Nitrogen oxides as nitrogen dioxide",      PollutantMasterUrl = "9"      },
                new() { PollutantName = "Carbon monoxide",                          PollutantMasterUrl = "10"     },
                new() { PollutantName = "Nitric oxide",                             PollutantMasterUrl = "38"     },
                new() { PollutantName = "Particulate calcium",                      PollutantMasterUrl = "629"    },
                new() { PollutantName = "Particulate chloride",                     PollutantMasterUrl = "631"    },
                new() { PollutantName = "Particulate magnesium",                    PollutantMasterUrl = "659"    },
                new() { PollutantName = "Particulate sodium",                       PollutantMasterUrl = "668"    },
                new() { PollutantName = "Particulate nitrite",                      PollutantMasterUrl = "999004" },
                new() { PollutantName = "Particulate nitrate",                      PollutantMasterUrl = "46"     },
                new() { PollutantName = "Particulate sulphate",                     PollutantMasterUrl = "47"     },
                new() { PollutantName = "Gaseous hydrochloric acid",                PollutantMasterUrl = "39"     },
                new() { PollutantName = "Gaseous nitric acid",                      PollutantMasterUrl = "50"     },
                new() { PollutantName = "Gaseous nitrous acid",                     PollutantMasterUrl = "999005" },
                new() { PollutantName = "Calcium in precipitation",                 PollutantMasterUrl = "630"    },
                new() { PollutantName = "Chloride in precipitation",                PollutantMasterUrl = "632"    },
                new() { PollutantName = "Potassium in precipitation",               PollutantMasterUrl = "658"    },
                new() { PollutantName = "Magnesium in precipitation",               PollutantMasterUrl = "660"    },
                new() { PollutantName = "Sodium in precipitation",                  PollutantMasterUrl = "669"    },
                new() { PollutantName = "Phosphate as P in precipitation",          PollutantMasterUrl = "2770"   },
                new() { PollutantName = "Nitrate as N in precipitation",            PollutantMasterUrl = "666"    },
                new() { PollutantName = "Ammonium as N in precipitation",           PollutantMasterUrl = "664"    },
                new() { PollutantName = "Sulphate as S in precipitation",           PollutantMasterUrl = "719"    },
                new() { PollutantName = "Non-marine sulphate as S in precipitation",PollutantMasterUrl = "720"    },
                new() { PollutantName = "Acidity in precipitation",                 PollutantMasterUrl = "648"    },
                new() { PollutantName = "Conductivity",                             PollutantMasterUrl = "412"    },
                new() { PollutantName = "pH in precipitation",                      PollutantMasterUrl = "753"    },
                new() { PollutantName = "Rainfall",                                 PollutantMasterUrl = "2076"   },
                new() { PollutantName = "Fluoride",                                 PollutantMasterUrl = "999006" },
                new() { PollutantName = "1,3-butadiene",                            PollutantMasterUrl = "24"     },
                new() { PollutantName = "Benzene",                                  PollutantMasterUrl = "20"     },
                new() { PollutantName = "Gaseous ammonia (passive)",                PollutantMasterUrl = "35"     },
                new() { PollutantName = "Gaseous ammonia (active)",                 PollutantMasterUrl = "35"     },
                new() { PollutantName = "Gaseous ammonia (diffusion tube)",         PollutantMasterUrl = "35"     },
                new() { PollutantName = "Particulate ammonium",                     PollutantMasterUrl = "45"     },
                new() { PollutantName = "Biphenyl",                                 PollutantMasterUrl = "7390"   },
                new() { PollutantName = "Cholanthrene",                             PollutantMasterUrl = "5526"   },
                new() { PollutantName = "Chrysene",                                 PollutantMasterUrl = "5406"   },
                new() { PollutantName = "Coronene",                                 PollutantMasterUrl = "5415"   },
                new() { PollutantName = "Cyclopenta(c,d)pyrene",                    PollutantMasterUrl = "5417"   },
                new() { PollutantName = "Dibenzo(al)pyrene",                        PollutantMasterUrl = "5528"   },
                new() { PollutantName = "Dibenzo(ae)pyrene",                        PollutantMasterUrl = "5636"   },
                new() { PollutantName = "Dibenzo(ai)pyrene",                        PollutantMasterUrl = "5639"   },
                new() { PollutantName = "Dibenzo(ah)pyrene",                        PollutantMasterUrl = "5637"   },
                new() { PollutantName = "Fluoranthene",                             PollutantMasterUrl = "5643"   },
                new() { PollutantName = "Fluorene",                                 PollutantMasterUrl = "7435"   },
                new() { PollutantName = "Perylene",                                 PollutantMasterUrl = "5488"   },
                new() { PollutantName = "Phenanthrene",                             PollutantMasterUrl = "5712"   },
                new() { PollutantName = "Pyrene",                                   PollutantMasterUrl = "5715"   },
                new() { PollutantName = "Retene",                                   PollutantMasterUrl = "999011" },
                new() { PollutantName = "Benzo(a)pyrene",                           PollutantMasterUrl = "5029"   },
                new() { PollutantName = "Benzo(a)anthracene",                       PollutantMasterUrl = "5610"   },
                new() { PollutantName = "Benzo(b)fluoranthene",                     PollutantMasterUrl = "5617"   },
                new() { PollutantName = "Benzo(j)fluoranthene",                     PollutantMasterUrl = "5759"   },
                new() { PollutantName = "Benzo(b+j)fluoranthene",                   PollutantMasterUrl = "7480"   },
                new() { PollutantName = "Benzo(k)fluoranthene",                     PollutantMasterUrl = "5626"   },
                new() { PollutantName = "Indeno(1,2,3-cd)pyrene",                   PollutantMasterUrl = "5655"   },
                new() { PollutantName = "Dibenzo(ac)anthracene",                    PollutantMasterUrl = "5527"   },
                new() { PollutantName = "Dibenzo(ah)anthracene",                    PollutantMasterUrl = "5419"   },
                new() { PollutantName = "Dibenzo(ah+ac)anthracene",                 PollutantMasterUrl = "7418"   },
                new() { PollutantName = "1-Methyl anthracene",                      PollutantMasterUrl = "7301"   },
                new() { PollutantName = "1-Methyl Naphthalene",                     PollutantMasterUrl = "7309"   },
                new() { PollutantName = "1-Methyl phenanthrene",                    PollutantMasterUrl = "7310"   },
                new() { PollutantName = "2-Methyl anthracene",                      PollutantMasterUrl = "7313"   },
                new() { PollutantName = "2-Methyl Naphthalene",                     PollutantMasterUrl = "7315"   },
                new() { PollutantName = "2-Methyl phenanthrene",                    PollutantMasterUrl = "7317"   },
                new() { PollutantName = "4.5-Methylene phenanthrene",               PollutantMasterUrl = "7521"   },
                new() { PollutantName = "5-Methyl Chrysene",                        PollutantMasterUrl = "5522"   },
                new() { PollutantName = "9-Methyl anthracene",                      PollutantMasterUrl = "7523"   },
                new() { PollutantName = "Acenaphthene",                             PollutantMasterUrl = "7351"   },
                new() { PollutantName = "Acenaphthylene",                           PollutantMasterUrl = "7352"   },
                new() { PollutantName = "Anthanthrene",                             PollutantMasterUrl = "5364"   },
                new() { PollutantName = "Anthracene",                               PollutantMasterUrl = "5606"   },
                new() { PollutantName = "Benzo(b)naphtho(2,1-d)thiophene",          PollutantMasterUrl = "5524"   },
                new() { PollutantName = "Benzo(c)phenanthrene",                     PollutantMasterUrl = "5525"   },
                new() { PollutantName = "Benzo(e)pyrene",                           PollutantMasterUrl = "5381"   },
                new() { PollutantName = "Benzo(ghi)perylene",                       PollutantMasterUrl = "5623"   },
                new() { PollutantName = "Naphthalene",                              PollutantMasterUrl = "7465"   }
            };

            var filterList = (filter ?? string.Empty)
                .Split(',')
                .Select(f => f.Trim())
                .ToList();

            var filtered = allPollutants
                .Where(p => filterList.Contains(p.PollutantName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            return filtered.Count > 0 ? filtered : allPollutants;
        }
    }
}
