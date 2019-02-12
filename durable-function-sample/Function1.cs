using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace durable_function_sample
{
    public static class Function1
    {
        [FunctionName("DurableFunctionsOrchestratorCS")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var instanceId = context.InstanceId;

            var step1 = context.WaitForExternalEvent("step1");
            var step2 = context.WaitForExternalEvent("step2");

            await Task.WhenAll(step1, step2);


            await context.CallActivityAsync<string>("FireNotification", instanceId);

            return $"Completed {instanceId}";
        }

        [FunctionName("FireNotification")]
        public static string FireNotification([ActivityTrigger] string id, ILogger log)
        {
            log.LogInformation($"****Firing notification for instance '{id}'") ;

            return $"Hello {id}!";
        }

        [FunctionName("handleNotification")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {

            var query = req.RequestUri.ParseQueryString();

            var id = query["id"];
            var stepName = query["stepname"];

            log.LogInformation($"*** Starting trigger. Id {id}, stepName {stepName}");

            var instance = await client.GetStatusAsync(id);
            if (instance == null)
            {
                // no instance with id "id" currently, so create one
                log.LogInformation($"**** Creating new orchestration with ID = '${id}'.");
                await client.StartNewAsync("DurableFunctionsOrchestratorCS", instanceId: id, input: null);
                log.LogInformation($"**** Created new orchestration with ID = '${id}'.");
            }
            else
            {
                // already have an instance with id "id"
                log.LogInformation($"****Found orchestration with ID = '{id}', { instance.RuntimeStatus}");
            }

            log.LogInformation($"****Raising event for orchestration with ID = '{id}', stepName='{stepName}''.");
            await client.RaiseEventAsync(id, stepName);

            return client.CreateCheckStatusResponse(req, id);
        }
    }
}