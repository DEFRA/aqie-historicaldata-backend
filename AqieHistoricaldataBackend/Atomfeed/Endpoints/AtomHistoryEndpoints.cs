using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Example.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Endpoints
{
    [ExcludeFromCodeCoverage]
    public static class AtomHistoryEndpoints
    {       
        public static void UseServiceAtomHistoryEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("AtomHistoryHourlydata", GetHistorydataById);
            app.MapPost("AtomHistoryHourlydata", GetHistorydataById);
            app.MapPost("AtomHistoryexceedence", GetHistoryexceedence);
            app.MapPost("AtomDataSelection", GetAtomDataSelection);
            app.MapPost("AtomDataSelectionJobStatus", GetAtomDataSelectionJobStatus);
            app.MapPost("AtomEmailJobDataSelection", GetAtomemailjobDataSelection);
            app.MapPost("AtomDataSelectionPresignedUrlMail", GetAtomDataSelectionPresignedUrlMail);
            app.MapPost("AtomDataSelectionNonAurnNetworks", GetAtomDataSelectionNonAurnNetworks);
            app.MapGet("AtomDataSelectionPollutantMaster", GetAtomDataSelectionPollutantMaster);
            app.MapPost("AtomDataSelectionPollutantDataSource", GetAtomDataSelectionPollutantDataSource);            
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
                logger.LogError(ex,"Error GetHistorydataById endpoints Info message {Error}", ex);
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
                logger.LogError(ex,"Error GetHistoryexceedence endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetAtomDataSelection([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomDataSelectionresult = await Persistence.GetatomDataSelectiondata(data);
                    return atomDataSelectionresult is not null ? Results.Ok(atomDataSelectionresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Error GetAtomDataSelection endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetAtomDataSelectionJobStatus([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomDataSelectionJobStatusresult = await Persistence.GetAtomDataSelectionJobStatusdata(data);
                    return atomDataSelectionJobStatusresult is not null ? Results.Ok(atomDataSelectionJobStatusresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Error GetAtomDataSelectionJobStatus endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }

        private static async Task<IResult> GetAtomemailjobDataSelection([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomemailjobDataSelectionresult = await Persistence.GetAtomemailjobDataSelection(data);
                    return atomemailjobDataSelectionresult is not null ? Results.Ok(atomemailjobDataSelectionresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Error GetAtomemailjobDataSelection endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }

        private static async Task<IResult> GetAtomDataSelectionPresignedUrlMail([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomDataSelectionJobStatusresult = await Persistence.GetAtomDataSelectionPresignedUrlMail(data);
                    return atomDataSelectionJobStatusresult is not null ? Results.Ok(atomDataSelectionJobStatusresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Error GetAtomDataSelectionPresignedUrlMail endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetAtomDataSelectionNonAurnNetworks([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomDataSelectionNonAurnNetworksresult = await Persistence.GetAtomDataSelectionNonAurnNetworks(data);
                    return atomDataSelectionNonAurnNetworksresult is not null ? Results.Ok(atomDataSelectionNonAurnNetworksresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Error GetAtomDataSelectionNonAurnNetworks endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetAtomDataSelectionPollutantMaster(IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {

                    var atompollutantmasterresult = await Persistence.GetAtomPollutantMaster();
                    return atompollutantmasterresult is not null ? Results.Ok(atompollutantmasterresult) : Results.NotFound();

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error GetAtomDataSelectionPollutantMaster endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }
        private static async Task<IResult> GetAtomDataSelectionPollutantDataSource([FromBody] QueryStringData data, IAtomHistoryService Persistence, ILogger<AtomHistoryService> logger)
        {
            try
            {
                if (data is not null)
                {
                    var atomDataSelectionPollutantDataSourceresult = await Persistence.GetAtomDataSelectionPollutantDataSource(data);
                    return atomDataSelectionPollutantDataSourceresult is not null ? Results.Ok(atomDataSelectionPollutantDataSourceresult) : Results.NotFound();
                }
                else
                {
                    return Results.NotFound();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error GetAtomDataSelectionPollutantDataSource endpoints Info message {Error}", ex);
                return Results.NotFound();

            }
        }
    }
}
