using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Example.Services;
using Microsoft.AspNetCore.Mvc;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Endpoints
{
    public static class AtomHistoryEndpoints
    {       
        public static void UseServiceAtomHistoryEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("AtomHistoryHealthchecks", GetHealthcheckdata);
            app.MapGet("AtomHistoryHourlydata", GetHistorydataById);
            app.MapPost("AtomHistoryHourlydata", GetHistorydataById);
            app.MapPost("AtomHistoryexceedence", GetHistoryexceedence);
        }
        private static async Task<IResult> GetHealthcheckdata(IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                var matches = await Persistence.AtomHealthcheck();
                return Results.Ok(matches);
            }
            catch (Exception ex)
            {
                logger.LogError("Error GetHealthcheckdata endpoints Info message {Error}", ex.Message);
                logger.LogError("Error GetHealthcheckdata endpoints Info stacktrace {Error}", ex.StackTrace);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetHistorydataById([FromBody] QueryStringData data,IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomhourlyresult = await Persistence.GetAtomHourlydata(data);
                    return atomhourlyresult is not null ? Results.Ok(atomhourlyresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch(Exception ex)
            {
                logger.LogError("Error GetHistorydataById endpoints Info message {Error}", ex.Message);
                logger.LogError("Error GetHistorydataById endpoints Info stacktrace {Error}", ex.StackTrace);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetHistoryexceedence([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomhourlyresult = await Persistence.GetHistoryexceedencedata(data);
                    return atomhourlyresult is not null ? Results.Ok(atomhourlyresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error GetHistorydataById endpoints Info message {Error}", ex.Message);
                logger.LogError("Error GetHistorydataById endpoints Info stacktrace {Error}", ex.StackTrace);
                return Results.NotFound();

            }
        }
    }
}
