﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.Common.Authentication;
using Microsoft.Azure.Commands.Common.Authentication.Abstractions;
using Microsoft.Azure.Commands.Common.Authentication.Properties;
using Microsoft.WindowsAzure.Commands.Utilities.Common;
using Newtonsoft.Json;
using System;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.Azure.Commands.AnalysisServices.Dataplane.Models
{
    /// <summary>
    /// The base class for all Microsoft Azure Analysis Services Management cmdlets
    /// </summary>
    public abstract class AsAzureDataplaneCmdletBase : AzurePSCmdlet
    {
        private AsAzureDataplaneClient _asAzureDataplaneClient;

        private IAzureContext _currentContext;

        /// <summary>
        /// The fully qualified absolute URI of the server instance.
        /// (scheme.region.azureAsEndpoint/serverName)
        /// </summary>
        /// <example>asazure://westus.asazure.windows.net/testserver</example>
        protected string ServerUri;

        /// <summary>
        /// A DNS safe host name for the server.
        /// (region.azureAsEndpoint)
        /// </summary>
        /// <example>westus.asazure.windows.net</example>
        protected string DnsSafeHost;

        /// <summary>
        /// The name of the server.
        /// </summary>
        /// <example>testserver</example>
        protected string ServerName;

        protected override IAzureContext DefaultContext
        {
            get
            {
                // Nothing to do with Azure Resource Management context
                return null;
            }
        }

        protected IAzureContext CurrentContext
        {
            get
            {
                if (_currentContext == null)
                {
                    _currentContext = AzureRmProfileProvider.Instance.Profile.DefaultContext;
                }

                return _currentContext;
            }

            set { _currentContext = value; }
        }

        public AsAzureDataplaneClient AsAzureDataplaneClient
        {
            get
            {
                if (_asAzureDataplaneClient == null)
                {
                    // TODO: Use AzureEnvironment.Endpoint.AzureAnalysisServicesEndpointSuffix for endpoint.
                    _asAzureDataplaneClient = CreateAsAzureDataplaneClient(DnsSafeHost, CurrentContext, () => { return new HttpClient(); });
                }

                return _asAzureDataplaneClient;
            }

            set { _asAzureDataplaneClient = value; }
        }

        protected void CommonBeginProcessing(string instance)
        {
            // user must specify the fully qualified server name. For example, westus2.asazure.windows.net/testserver
            if (!Uri.TryCreate(instance, UriKind.Absolute, out var uriResult) || uriResult.Scheme != AsAzureEndpoints.UriSchemeAsAzure)
            {
                throw new PSInvalidOperationException(string.Format(Properties.Resources.InvalidServerName, instance));
            }

            // derive all bits of the url from the input
            ServerUri = uriResult.AbsoluteUri;
            DnsSafeHost = uriResult.DnsSafeHost;
            ServerName = uriResult.PathAndQuery.Trim('/');

            AsAzureDataplaneClient = CreateAsAzureDataplaneClient(DnsSafeHost, CurrentContext, () => { return new HttpClient(); });
        }

        protected ClusterResolutionResult ClusterResolve(Uri clusterUri, string serverName)
        {
            var content = new StringContent($"ServerName={serverName}");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

            this.AsAzureDataplaneClient.resetHttpClient();
            using (var message = AsAzureDataplaneClient.CallPostAsync(
                baseUri: clusterUri,
                requestUrl: AsAzureEndpoints.ClusterResolveEndpoint,
                content: content).Result)
            {
                message.EnsureSuccessStatusCode();
                var rawResult = message.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<ClusterResolutionResult>(rawResult);
            }
        }

        internal static AsAzureDataplaneClient CreateAsAzureDataplaneClient(string endpoint, IAzureContext context, Func<HttpClient> httpClientProvider, bool parameterizedBaseUri = false)
        {
            if (context == null)
            {
                throw new ApplicationException(Resources.NoSubscriptionInContext);
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            var baseUri = new Uri(string.Format("{0}{1}{2}", Uri.UriSchemeHttps, Uri.SchemeDelimiter, endpoint));
            var credentials = AzureSession.Instance.AuthenticationFactory.GetServiceClientCredentials(context, endpoint);
            var handlers = AzureSession.Instance.ClientFactory.GetCustomHandlers();
            return AzureSession.Instance.ClientFactory.CreateCustomArmClient<AsAzureDataplaneClient>(baseUri, credentials, httpClientProvider, handlers);
        }
    }
}
