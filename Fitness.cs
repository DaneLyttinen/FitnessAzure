using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Console;
using Carter;
using Carter.ModelBinding;
using Carter.Request;
using Carter.Response;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json.Linq;
using System.Net;

namespace FitnessApp
{
    public static class Fitness
    {
        [FunctionName("target")]
        public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, [DurableClient] IDurableEntityClient client,
    ILogger log)
        {
            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
           
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            
            var entityId = new EntityId("Message", "myMessage");
            await client.SignalEntityAsync(entityId, "Add", data.target);
            await client.SignalEntityAsync(entityId, "AddParallel", data.parallel);

           

            //var t = await data.Bind<TargetRequest>();
            return new OkObjectResult("works");
        }
        [FunctionName("assess")]
        public static async Task<IActionResult> Rune(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, [DurableClient] IDurableEntityClient client,
    ILogger log)
        {
            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            AssessRequest areq = System.Text.Json.JsonSerializer.Deserialize<AssessRequest>(requestBody);
            
            var genomes = areq.genomes;

            TargetRequest t;
            var target = "";
            var parallel = false;
            var entityId = new EntityId("Message", "myMessage");
            var message = await client.ReadEntityStateAsync<Message>(entityId);
            target = message.EntityState.Get();
            parallel = message.EntityState.GetParallel();

            Func<string, int> assess = g => {
                var len = Math.Min(target.Length, g.Length);
                var h = Enumerable.Range(0, len)
                    .Sum(i => Convert.ToInt32(target[i] != g[i]));
                h = h + Math.Max(target.Length, g.Length) - len;
                return h;
            };

            var scores = parallel ?
                genomes.AsParallel().Select(assess).ToList() :
                genomes.Select(assess).ToList();


           var min = scores.DefaultIfEmpty().Min();


            
            var ares = new AssessResponse { id = areq.id, scores = scores };
            return new JsonResult(ares);
            
        }


        public class AssessRequest
        {
            public int id { get; set; }
            public List<string> genomes { get; set; }
            public override string ToString()
            {
                return $"{{{id}, #{genomes.Count}}}";
            }
        }

        public class AssessResponse
        {
            public int id { get; set; }
            public List<int> scores { get; set; }
            public override string ToString()
            {
                return $"{{{id}, #{scores.Count}}}";
            }
        }
    }
    public class TargetRequest
    {
        public int id { get; set; }
        public bool parallel { get; set; }
        public string target { get; set; }
        public override string ToString()
        {
            return $"{{{id}, {parallel}, \"{target}\"}}";
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Message
    {
        [JsonProperty("target")]
        private string CurrentValue { get; set; }

        [JsonProperty("parallel")]
        private bool parallel { get; set; }
        public void Reset() => this.CurrentValue = null;

        public void Add(string target) => this.CurrentValue = target;

        public void AddParallel(bool parallel) => this.parallel = parallel;

        public string Get() => this.CurrentValue;

        public bool GetParallel() => this.parallel;

        [FunctionName(nameof(Message))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<Message>();
    }
}
