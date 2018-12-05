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

using System;
using System.Management.Automation;
using System.Security.Permissions;
using Microsoft.Azure.Commands.Kusto.Models;
using Microsoft.Azure.Commands.Kusto.Properties;
using Microsoft.Azure.Commands.Kusto.Utilities;
using Microsoft.Rest.Azure;
using Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters;

namespace Microsoft.Azure.Commands.Kusto.Commands
{

    [Cmdlet("Update", ResourceManager.Common.AzureRMConstants.AzureRMPrefix + "KustoCluster",
         SupportsShouldProcess = true),
     OutputType(typeof(PSKustoCluster))]
    public class UpdateAzureRmKustoCluster : KustoCmdletBase
    {
        protected const string CmdletParametersSet = "ByNameAndResourceGroup";
        protected const string ObjectParameterSet = "ByInputObject";
        protected const string ResourceIdParameterSet = "ByResourceId";

        [Parameter(
            ParameterSetName = CmdletParametersSet,
            Mandatory = true,
            HelpMessage = "Name of cluster to be updated.")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(
            ParameterSetName = CmdletParametersSet,
            Mandatory = false,
            HelpMessage = "Name of resource group under which the cluster exists.")]
        [ValidateNotNullOrEmpty]
        public string ResourceGroupName { get; set; }

        [Parameter(
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = CmdletParametersSet,
            Mandatory = true,
            HelpMessage = "Azure region where the cluster exists.")]
        [LocationCompleter("Microsoft.Kusto/clusters")]
        public string Location { get; set; }

        [Parameter(
            ValueFromPipelineByPropertyName = true,
            Mandatory = true,
            HelpMessage = "Name of the Sku used to create the cluster")]
        [ValidateNotNullOrEmpty]
        [ValidateSet("KC8", "KC16", "KS8", "KS16", "L8", "L16", "D14_v2", "D13_v2", IgnoreCase = true)]
        public string SkuName { get; set; }

        [Parameter(
            ValueFromPipelineByPropertyName = true,
            Mandatory = false,
            HelpMessage = "Name of the Tier used to create the cluster")]
        [ValidateSet("Standard", IgnoreCase = true)]
        public string Tier { get; set; }
        
        [Parameter(
            ParameterSetName = ResourceIdParameterSet,
            Mandatory = true,
            Position = 1,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Kusto cluster ResourceID.")]
        [ValidateNotNullOrEmpty]
        public string ResourceId { get; set; }

        [Parameter(
            ParameterSetName = ObjectParameterSet,
            Mandatory = true,
            Position = 2,
            ValueFromPipeline = true,
            HelpMessage = "Kusto cluster object.")]
        [ValidateNotNullOrEmpty]
        public PSKustoCluster InputObject { get; set; }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public override void ExecuteCmdlet()
        {
            string clusterName = Name;
            string resourceGroupName = ResourceGroupName;
            string location = Location;

            if (!string.IsNullOrEmpty(ResourceId))
            {
                KustoUtils.GetResourceGroupNameAndClusterNameFromClusterId(ResourceId, out resourceGroupName, out clusterName);
            }
            else if (InputObject != null)
            {
                KustoUtils.GetResourceGroupNameAndClusterNameFromClusterId(InputObject.Id, out resourceGroupName, out clusterName);
            }

            if (string.IsNullOrEmpty(clusterName))
            {
                WriteExceptionError(new PSArgumentNullException("Name", "Name of cluster not specified"));
            }

            if (ShouldProcess(clusterName, Resources.UpdatingKustoCluster))
            {
                try
                {
                    var cluster = KustoClient.GetCluster(resourceGroupName, clusterName);
                    if (cluster == null)
                    {
                        throw new CloudException(string.Format(Resources.KustoClusterNotExist, Name));
                    }

                    if (location != null && location != cluster.Location)
                    {
                        throw new CloudException(string.Format(Resources.KustoClusterNotExist, Name, cluster.Location));
                    }
                    location = cluster.Location;
                }
                catch (CloudException ex)
                {
                    if (ex.Body != null && !string.IsNullOrEmpty(ex.Body.Code) && ex.Body.Code == "ResourceNotFound" ||
                        ex.Message.Contains("ResourceNotFound"))
                    {
                        throw new CloudException(string.Format(Resources.KustoClusterNotExist, Name));
                    }
                    else if (ex.Body != null && !string.IsNullOrEmpty(ex.Body.Code) &&
                             ex.Body.Code == "ResourceGroupNotFound" || ex.Message.Contains("ResourceGroupNotFound"))
                    {
                        // resource group not found, let create throw error don't throw from here
                    }
                    else
                    {
                        // all other exceptions should be thrown
                        throw;
                    }
                }

                var updatedCluster = KustoClient.CreateOrUpdateCluster(resourceGroupName, clusterName, location, SkuName);
                WriteObject(updatedCluster);
            }
        }

    }
}