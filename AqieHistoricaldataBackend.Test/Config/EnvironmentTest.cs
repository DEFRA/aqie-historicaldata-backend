using Microsoft.AspNetCore.Builder;

namespace AqieHistoricaldataBackend.Test.Config;

public class EnvironmentTest
{

   [Fact]
   public void IsNotDevModeByDefault()
   { 
       var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
       var isDev = AqieHistoricaldataBackend.Config.Environment.IsDevMode(builder);
       Assert.False(isDev);
   }
}
