using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Newtonsoft.Json;
using Amazon.Lambda.Core;

namespace LambdaSNSWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SNSController : Controller
    {
        private AmazonSimpleNotificationServiceClient _SnsClient { get; set; }
        private ListTopicsRequest _SnsRequest { get; set; }
        private ListTopicsResponse _SnsResponse { get; set; }
        private ConfirmSubscriptionResponse _confirmSubscriptionResponse { get; set; }
        private BasicAWSCredentials AwsCredentials { get; set; }

        private const string AccessKey = "AKIAJ3DUDKS2PF7ICLXA";
        private const string SecretKey = "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";


        //@ TODO : Need to find a way for DI for AmazonSimpleNotificationServiceClient
        public SNSController()
        {
            AwsCredentials = new Amazon.Runtime.BasicAWSCredentials(AccessKey, SecretKey);
        }

        /// <summary>
        /// POST: api/sns
        /// ------------------------------------------------------------------------------------
        /// This method handles POST request from Amazon SNS
        /// ------------------------------------------------------------------------------------
        /// </summary>
        /// <returns>IActionResult</returns>
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            string jsonData = null;
            string status = null;
            if (Request.Body.CanRead)
            {
                try
                {
                    using (var reader = new StreamReader(Request.Body))
                    {
                        jsonData = reader.ReadToEnd();
                    }
                }
                catch (IOException ioex)
                {
                    status = ioex.ToString();
                }
            }

            var sm = Amazon.SimpleNotificationService.Util.Message.ParseMessage(jsonData);
            
            LambdaLogger.Log(sm.ToString());
            if (sm.IsMessageSignatureValid())
            {
                if (sm.IsSubscriptionType)
                {
                    // CONFIRM THE SUBSCRIPTION
                    using (_SnsClient = new AmazonSimpleNotificationServiceClient(AwsCredentials, RegionEndpoint.USEast2))
                    {
                        try
                        {
                            _confirmSubscriptionResponse = await _SnsClient.ConfirmSubscriptionAsync(request: new ConfirmSubscriptionRequest
                            {
                                TopicArn = sm.TopicArn,
                                Token = sm.Token
                            });
                            status = _confirmSubscriptionResponse.SubscriptionArn;
                            LambdaLogger.Log(status);
                        }
                        catch (AmazonSimpleNotificationServiceException ex)
                        {
                            return Content(HandleSNSError(ex));
                        }
                    }
                }
                if (sm.IsNotificationType) 
                {
                    // PROCESS NOTIFICATIONS
                    status = "SNS Subject: " + sm.Subject + " | SNS Message: " + sm.MessageText;
                    LambdaLogger.Log(status);
                }
            }
            if(status == null)
            {
                status = "MicroService executed with invalid signature and data: \n" + jsonData;
            }
            return Ok(status);
        }

        /// <summary>
        /// Get: api/sns
        /// ------------------------------------------------------------------------------------
        /// This method handles the Get Request to fetch List of Topics in Amazon SNS 
        /// ------------------------------------------------------------------------------------
        /// </summary>
        /// <returns>
        /// IActionResult
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var topicList = new List<Dictionary<string, string>>();
            Dictionary<string, string> dictAttributes = null;
            using (_SnsClient = new AmazonSimpleNotificationServiceClient(AwsCredentials, RegionEndpoint.USEast2))
            {
                try
                {
                    _SnsRequest = new ListTopicsRequest();
                    do
                    {
                        _SnsResponse = await _SnsClient.ListTopicsAsync(_SnsRequest);
                        foreach (var topic in _SnsResponse.Topics)
                        {
                            // Get topic attributes
                            var topicAttributes = await _SnsClient.GetTopicAttributesAsync(request: new GetTopicAttributesRequest
                            {
                                TopicArn = topic.TopicArn
                            });

                            // this is see attributes of topic
                            if (topicAttributes.Attributes.Count > 0)
                            {
                                dictAttributes = new Dictionary<string, string>();
                                foreach (var topicAttribute in topicAttributes.Attributes)
                                {
                                    dictAttributes.Add(topicAttribute.Key, topicAttribute.Value.ToString());
                                }
                                topicList.Add(dictAttributes);
                            }
                        }
                        _SnsRequest.NextToken = _SnsResponse.NextToken;

                    } while (_SnsResponse.NextToken != null);
                }
                catch (AmazonSimpleNotificationServiceException ex)
                {
                    return Content(HandleSNSError(ex));
                }
            }

            return Ok(JsonConvert.SerializeObject(topicList));
        }

        #region Private Method
        private string HandleSNSError(AmazonSimpleNotificationServiceException ex)
        {
            StringBuilder sb = new StringBuilder("Some Unexpected Error occured \n\n ");
            sb.Append("Caught Exception: " + ex.Message)
            .Append(" | Response Status Code: " + ex.StatusCode)
            .Append(" | Error Code: " + ex.ErrorCode)
            .Append(" | Error Type: " + ex.ErrorType)
            .Append(" | Request ID: " + ex.RequestId);

            return sb.ToString();
        }
        #endregion
    }
}
