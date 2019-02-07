using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace OneRosterSync.Net.Extensions
{
	public class AlertDecoratorResult : IActionResult
	{
		public IActionResult Result { get; }
		public string Type { get; }
		public string Body { get; }

		public AlertDecoratorResult(IActionResult result, string type, string body)
		{
			Result = result;
			Type = type;
			Body = body;
		}

		public async Task ExecuteResultAsync(ActionContext context)
		{
			if (Result is StatusCodeResult || Result is OkObjectResult)
			{
				AddAlertMessageToApiResult(context);
			}
			else
			{
				AddAlertMessageToMvcResult(context);
			}

			await Result.ExecuteResultAsync(context);
		}

		private void AddAlertMessageToApiResult(ActionContext context)
		{
			context.HttpContext.Response.Headers.Add("x-alert-type", Type);
			context.HttpContext.Response.Headers.Add("x-alert-body", Body);
		}

		private void AddAlertMessageToMvcResult(ActionContext context)
		{
			var factory = context.HttpContext.RequestServices.GetService<ITempDataDictionaryFactory>();

			var tempData = factory.GetTempData(context.HttpContext);
			tempData["_alert.type"] = Type;
			tempData["_alert.body"] = Body;
		}
	}
}
