using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Polaris.Utilities.Logging;
using RepositoryDataAccessLayer.Helper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Polaris.RepositoryDataAccessLayer
{
    public class RepositoryDataAccess : IRepository
    {
        #region Local variagles
        private static DocumentClient client;
        #endregion
        public RepositoryDataAccess(string endpointUri, string primaryKey)
        {
            if (client == null)
            {
                //Thread safe singleton
                lock (typeof(RepositoryDataAccess))
                {
                    client = new DocumentClient(new Uri(endpointUri), primaryKey);
                }
            }
        }

        #region Public properties
        public int MaxRetries { get; set; }

        public int MaxItemCount { get; set; }

        // Splunk variables
        public string SystemUser { get; set; }

        public string ApplicationName { get; set; }

        public string ServerName { get; set; }

        public string SplunkTarget { get; set; }

        public string SplunkToken { get; set; }

        public bool LogIfInfo { get; set; }
        #endregion

        #region Public methods
        public T Insert<T>(string collectionName, T item)
        {
            try
            {
                var rawResponse = ExecuteWithRetries(client, () => client.CreateDocumentAsync(collectionName, item).Result);
                var response = JsonConvert.SerializeObject(rawResponse.Resource);
                return JsonConvert.DeserializeObject<T>(response);
            }
            catch
            {
                throw;
            }
        }

        public T Update<T>(string collectionName, T item, string id)
        {
            try
            {
                dynamic refDoc = ExecuteWithRetries(client, () => client.CreateDocumentQuery<Document>(collectionName).Where(d => d.Id == id).AsEnumerable().FirstOrDefault());

                if (refDoc != null)
                {
                    //Update document using self link.  
                    dynamic rawResponse = ExecuteWithRetries(client, () => client.ReplaceDocumentAsync(refDoc.SelfLink, item).Result);
                    var response = JsonConvert.SerializeObject(rawResponse.Resource);
                    return JsonConvert.DeserializeObject<T>(response);
                }
                else
                {
                    // Splunk initialization
                    var logger = new Logger(new Utilities.Logging.Environment
                    {
                        ApplicationName = ApplicationName,
                        ServerName = ServerName,
                        SplunkTarget = SplunkTarget,
                        SplunkToken = SplunkToken
                    });

                    //Log to Splunk
                    MethodBase methodBase = MethodBase.GetCurrentMethod();
                    logger.Log(string.Format("Class: {0}, Method: {1} ", methodBase.ReflectedType.Name, methodBase.Name) + "No record found. Id: " + id, Logger.Level.Error, SystemUser, string.Empty, null);
                }
            }
            catch
            {
                throw;
            }
            return default(T);
        }

        public T GetDataById<T>(string collectionName, string id)
        {
            try
            {
                // Feed options
                var feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = true,
                    MaxItemCount = MaxItemCount == 0 ? 500 : MaxItemCount // Default value : 500
                };

                dynamic doc = ExecuteWithRetries(client, () => client.CreateDocumentQuery<Document>(collectionName, feedOptions).Where(d => d.Id == id).AsEnumerable().FirstOrDefault());
                var response = JsonConvert.SerializeObject(doc);
                return JsonConvert.DeserializeObject<T>(response);
            }
            catch
            {
                throw;
            }
        }

        public List<T> GetDataByKeys<T>(string collectionName, List<Filter> searchItems)
        {
            try
            {
                // Feed options
                var feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = true,
                    MaxItemCount = MaxItemCount == 0 ? 500 : MaxItemCount // Default value : 500
                };

                // Build Where clause 
                var queryDelegate = ExpressionBuilder.GetExpression<T>(searchItems);
                return ExecWithRetries(client, () => client.CreateDocumentQuery<T>(collectionName, feedOptions).Where(queryDelegate).ToList());
            }
            catch
            {
                throw;
            }
        }

        public List<T> GetAllData<T>(string collectionName)
        {
            try
            {
                // Feed options
                var feedOptions = new FeedOptions
                {
                    EnableCrossPartitionQuery = true,
                    MaxItemCount = MaxItemCount == 0 ? 500 : MaxItemCount // Default value : 500
                };
                return ExecWithRetries(client, () => client.CreateDocumentQuery<T>(collectionName, feedOptions).ToList());
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Private methods
        private T ExecuteWithRetries<T>(DocumentClient client, Func<T> function)
        {
            TimeSpan sleepTime = TimeSpan.Zero;
            int Retries = 0;
            // 429 Retry option
            int maxItemCount = MaxItemCount == 0 ? 3 : MaxItemCount; // Default value : 3

            // Splunk initialization
            var logger = new Logger(new Utilities.Logging.Environment
            {
                ApplicationName = ApplicationName,
                ServerName = ServerName,
                SplunkTarget = SplunkTarget,
                SplunkToken = SplunkToken
            });

            while (true)
            {
                try
                {
                    if (Retries >= maxItemCount)
                    {
                        //Log to Splunk
                        MethodBase methodBase = MethodBase.GetCurrentMethod();
                        logger.Log(string.Format("Class: {0}, Method: {1} ", methodBase.ReflectedType.Name, methodBase.Name) + "Exceeded maximum retry count: " + Retries.ToString(), Logger.Level.Error, SystemUser, string.Empty, null);
                        throw new Exception("Errors: Request rate is large");
                    }
                    else
                        return function();
                }
                catch (DocumentClientException de)
                {
                    if (((int)de.StatusCode != 429) || ((int)de.StatusCode != 503))
                    {
                        throw;
                    }
                    sleepTime = de.RetryAfter;
                    Retries++;
                }
                catch (AggregateException ae)
                {
                    if (!(ae.InnerException is DocumentClientException))
                    {
                        throw;
                    }

                    DocumentClientException de = (DocumentClientException)ae.InnerException;
                    if (((int)de.StatusCode != 429) || ((int)de.StatusCode != 503))
                    {
                        throw;
                    }
                    sleepTime = de.RetryAfter;
                    Retries++;
                }

                Task.Delay(sleepTime);
            }
        }

        private List<T> ExecWithRetries<T>(DocumentClient client, Func<List<T>> function)
        {
            TimeSpan sleepTime = TimeSpan.Zero;
            int Retries = 0;
            // 429 Retry option
            int maxItemCount = MaxItemCount == 0 ? 3 : MaxItemCount; // Default value : 3

            // Splunk initialization
            var logger = new Logger(new Utilities.Logging.Environment
            {
                ApplicationName = ApplicationName,
                ServerName = ServerName,
                SplunkTarget = SplunkTarget,
                SplunkToken = SplunkToken
            });

            while (true)
            {
                try
                {
                    if (Retries >= maxItemCount)
                    {
                        //Log to Splunk
                        MethodBase methodBase = MethodBase.GetCurrentMethod();
                        logger.Log(string.Format("Class: {0}, Method: {1} ", methodBase.ReflectedType.Name, methodBase.Name) + "Exceeded maximum retry count: " + Retries.ToString(), Logger.Level.Error, SystemUser, string.Empty, null);
                        throw new Exception("Errors: Request rate is large");
                    }
                    else
                        return function();
                }
                catch (DocumentClientException de)
                {
                    if (((int)de.StatusCode != 429) || ((int)de.StatusCode != 503))
                    {
                        throw;
                    }
                    sleepTime = de.RetryAfter;
                    Retries++;
                }
                catch (AggregateException ae)
                {
                    if (!(ae.InnerException is DocumentClientException))
                    {
                        throw;
                    }

                    DocumentClientException de = (DocumentClientException)ae.InnerException;
                    if (((int)de.StatusCode != 429) || ((int)de.StatusCode != 503))
                    {
                        throw;
                    }
                    sleepTime = de.RetryAfter;
                    Retries++;
                }

                Task.Delay(sleepTime);
            }
        }
        #endregion
    }
}
