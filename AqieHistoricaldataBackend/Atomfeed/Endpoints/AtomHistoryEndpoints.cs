using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Example.Services;

namespace AqieHistoricaldataBackend.Atomfeed.Endpoints
{
    public static class AtomHistoryEndpoints
    {       
        public static void UseServiceAtomHistoryEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("AtomHistoryHealthchecks", GetHealthcheckdata);
            app.MapGet("AtomHistoryHourlydata/{name}", GetHistorydataById);

        }

        private static async Task<IResult> GetHealthcheckdata(IAtomHistoryService Persistence)
        {
            var matches = await Persistence.AtomHealthcheck();
            return Results.Ok(matches);
        }

        private static async Task<IResult> GetHistorydataById(string name, IAtomHistoryService Persistence)
        {
            if (name is not null && !string.IsNullOrWhiteSpace(name))
            {
                var atomhourlyresult = await Persistence.GetAtomHourlydata(name);
                //return Results.File(atomhourlyresult,)
                return atomhourlyresult is not null ? Results.Ok(atomhourlyresult) : Results.NotFound();
            }
            else
            {
                return Results.NotFound();
            }                
        }
    }
}
