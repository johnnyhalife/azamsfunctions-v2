using System;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace azamsfunctions
{
    public static class GenerateContentProtectionToken
    {
        [FunctionName("GenerateContentProtectionToken")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "content-protection-token")]HttpRequestMessage req, 
            TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            var restrictionPrimaryVerificationKey = Environment.GetEnvironmentVariable("JWTRestrictionPrimaryVerificationKey");
            var restrictionAudience = Environment.GetEnvironmentVariable("JWTRestrictionAudience");
            var restrictionIssuer = Environment.GetEnvironmentVariable("JWTRestrictionIssuer");
            
            var cred = new SigningCredentials(
               new InMemorySymmetricSecurityKey(Convert.FromBase64String(restrictionPrimaryVerificationKey)),
               "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256",
               "http://www.w3.org/2001/04/xmlenc#sha256");

            var jwtToken = new JwtSecurityToken(
                 issuer: restrictionIssuer,
                 audience: restrictionAudience,
                 notBefore: DateTime.UtcNow.AddMinutes(-1),
                 expires: DateTime.UtcNow.AddMinutes(10),
                 signingCredentials: cred);

            var handler = new JwtSecurityTokenHandler();

            return req.CreateResponse(HttpStatusCode.OK, handler.WriteToken(jwtToken));
        }
    }
}
