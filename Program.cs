// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ManageFunctionAppSourceControl
{
    public class Program
    {
        /**
         * Azure App Service basic sample for managing function apps.
         *  - Create 5 function apps under the same new app service plan:
         *    - Deploy to 1 using FTP
         *    - Deploy to 2 using local Git repository
         *    - Deploy to 3 using a publicly available Git repository
         *    - Deploy to 4 using a GitHub repository with continuous integration
         *    - Deploy to 5 using web deploy
         */

        public static async Task RunSample(ArmClient client)
        {
            // New resources
            AzureLocation region = AzureLocation.EastUS;
            string suffix         = ".azurewebsites.net";
            string appName        = Utilities.CreateRandomName("webapp1-");
            string app1Name       = Utilities.CreateRandomName("webapp1-");
            string app2Name       = Utilities.CreateRandomName("webapp2-");
            string app3Name       = Utilities.CreateRandomName("webapp3-");
            string app4Name       = Utilities.CreateRandomName("webapp4-");
            string app5Name       = Utilities.CreateRandomName("webapp5-");
            string function1Name = Utilities.CreateRandomName("function1-");
            string function2Name = Utilities.CreateRandomName("function2-");
            string function3Name = Utilities.CreateRandomName("function3-");
            string function4Name = Utilities.CreateRandomName("function4-");
            string function5Name = Utilities.CreateRandomName("function5-");
            string app1Url        = app1Name + suffix;
            string app2Url        = app2Name + suffix;
            string app3Url        = app3Name + suffix;
            string app4Url        = app4Name + suffix;
            string app5Url        = app5Name + suffix;
            string rgName         = Utilities.CreateRandomName("rg1NEMV_");
            var lro = await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;
            try {


                //============================================================
                // Create a function app with a new app service plan

                Utilities.Log("Creating function app " + app1Name + " in resource group " + rgName + "...");

                var webSiteCollection = resourceGroup.GetWebSites();
                var webSiteData = new WebSiteData(region)
                {
                };
                var webSite_lro =await webSiteCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, appName, webSiteData);
                var webSite = webSite_lro.Value;

                var planCollection = resourceGroup.GetAppServicePlans();
                var planData = new AppServicePlanData(region)
                {
                };
                var planResource_lro =await planCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, appName, planData);
                var planResource = planResource_lro.Value;

                SiteFunctionCollection functionAppCollection = webSite.GetSiteFunctions();
                var functionData = new FunctionEnvelopeData()
                {
                };
                var funtion_lro =await functionAppCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, function1Name, functionData);
                var function = funtion_lro.Value;

                Utilities.Log("Created function app " + function.Data.Name);
                Utilities.Print(function);

                //============================================================
                // Deploy to app 1 through FTP

                Utilities.Log("Deploying a function app to " + app1Name + " through FTP...");

                var csm = new CsmPublishingProfile()
                {
                    Format = PublishingProfileFormat.Ftp
                };
                var profile = await webSite.GetPublishingProfileXmlWithSecretsAsync(csm);
                Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "Asset", "square-function-app", "host.json"));
                Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "Asset", "square-function-app", "square", "function.json"), "square/function.json");
                Utilities.UploadFileToFunctionApp(profile, Path.Combine(Utilities.ProjectPath, "Asset", "square-function-app", "square", "index.js"), "square/index.js");

                // sync triggers
                webSite.SyncFunctionTriggers();

                Utilities.Log("Deployment square app to web app " + function.Data.Name + " completed");
                Utilities.Print(function);

                // warm up
                Utilities.Log("Warming up " + app1Url + "/api/square...");
                Utilities.PostAddress("http://" + app1Url + "/api/square", "625");
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app1Url + "/api/square...");
                Utilities.Log(Utilities.PostAddress("http://" + app1Url + "/api/square", "625"));

                //============================================================
                // Create a second function app with local git source control

                Utilities.Log("Creating another function app " + app2Name + " in resource group " + rgName + "...");
                var webSite2Collection = resourceGroup.GetWebSites();
                var webSite2Data = new WebSiteData(region)
                {
                    SiteConfig = new SiteConfigProperties()
                    {
                        AzureStorageAccounts = webSite.Data.SiteConfig.AzureStorageAccounts,
                    },
                    AppServicePlanId = planCollection.Id,
                    IsStorageAccountRequired = true,
                };
                var webSite2_lro = await webSite2Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, app2Name, webSite2Data);
                var webSite2 = webSite2_lro.Value;
                SiteFunctionCollection functionApp2Collection = webSite.GetSiteFunctions();
                var function2Data = new FunctionEnvelopeData()
                {
                };
                var funtion2_lro = await functionApp2Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, function2Name, function2Data);
                var function2 = funtion2_lro.Value;

                Utilities.Log("Created function app " + function2.Data.Name);
                Utilities.Print(function2);

                //============================================================
                // Deploy to app 2 through local Git

                Utilities.Log("Deploying a local Tomcat source to " + app2Name + " through Git...");

                var reader = new StreamReader(profile);
                var content = reader.ReadToEnd();
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(content);
                XmlNodeList gitUrl = xmlDoc.GetElementsByTagName("publishUrl");
                string gitUrlString = gitUrl[0].InnerText;
                XmlNodeList userName = xmlDoc.GetElementsByTagName("userName");
                string userNameString = userName[0].InnerText;
                XmlNodeList password = xmlDoc.GetElementsByTagName("userPWD");
                string passwordString = password[0].InnerText;
                Utilities.DeployByGit(userNameString, passwordString, gitUrlString, "azure-samples-appservice-helloworld");

                Utilities.Log("Deployment to function app " + webSite2.Data.Name + " completed");
                Utilities.Print(webSite2);

                // warm up
                Utilities.Log("Warming up " + app2Url + "/api/square...");
                Utilities.PostAddress("http://" + app2Url + "/api/square", "725");
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app2Url + "/api/square...");
                Utilities.Log("Square of 725 is " + Utilities.PostAddress("http://" + app2Url + "/api/square", "725"));

                //============================================================
                // Create a 3rd function app with a public GitHub repo in Azure-Samples

                Utilities.Log("Creating another function app " + app3Name + "...");
                var webSite3Data = new WebSiteData(region)
                {
                    SiteConfig = new SiteConfigProperties()
                    {
                        AzureStorageAccounts = webSite.Data.SiteConfig.AzureStorageAccounts,
                    },
                    AppServicePlanId = planCollection.Id,
                };
                var publicRepodata = new SiteSourceControlData()
                {
                    RepoUri = new Uri("https://github.com/jianghaolu/square-function-app-sample"),
                    Branch = "master",
                    //IsManualIntegration = true,
                    //IsMercurial = false,
                };
                var webSite3_lro = await webSiteCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, app3Name, webSite3Data);
                var webSite3 = webSite3_lro.Value;
                var container = webSite3.GetWebSiteSourceControl();
                var sourceControl = (await container.CreateOrUpdateAsync(Azure.WaitUntil.Completed, publicRepodata)).Value;
                var function3Collection = webSite3.GetSiteFunctions();
                var function3Data = new FunctionEnvelopeData()
                {
                };
                var function3_lro = await function3Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, function3Name, function3Data);
                var function3 = function3_lro.Value;

                Utilities.Log("Created function app " + function3.Data.Name);
                Utilities.Print(function3);

                // warm up
                Utilities.Log("Warming up " + app3Url + "/api/square...");
                Utilities.PostAddress("http://" + app3Url + "/api/square", "825");
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app3Url + "/api/square...");
                Utilities.Log("Square of 825 is " + Utilities.PostAddress("http://" + app3Url + "/api/square", "825"));

                //============================================================
                // Create a 4th function app with a personal GitHub repo and turn on continuous integration

                Utilities.Log("Creating another function app " + app4Name + "...");
                var webSite4Data = new WebSiteData(region)
                {
                    SiteConfig = new SiteConfigProperties()
                    {
                        AzureStorageAccounts = webSite.Data.SiteConfig.AzureStorageAccounts,
                    },
                    AppServicePlanId = planCollection.Id,
                };
                var webSite4_lro = await webSiteCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, function4Name, webSite4Data);
                var webSite4 = webSite4_lro.Value;
                var function4Collection = webSite4.GetSiteFunctions();
                var function4Data = new FunctionEnvelopeData()
                {
                };
                var function4_lro = await function4Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, app4Name, function4Data);
                var function4 = function4_lro.Value;

                Utilities.Log("Created function app " + function4.Data.Name);
                Utilities.Print(function4);

                // warm up
                Utilities.Log("Warming up " + app4Url + "...");
                Utilities.CheckAddress("http://" + app4Url);
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app4Url + "...");
                Utilities.Log(Utilities.CheckAddress("http://" + app4Url));

                //============================================================
                // Create a 5th function app with web deploy

                Utilities.Log("Creating another function app " + app5Name + "...");
                var function5Collection = webSite.GetSiteFunctions();
                var function5Data = new FunctionEnvelopeData()
                {
                };
                var function5_lro = await function5Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, function5Name, function5Data);
                var function5 = function5_lro.Value;

                Utilities.Log("Created function app " + function5.Data.Name);
                Utilities.Print(function5);

                Utilities.Log("Deploying to " + app5Name + " through web deploy...");
                var extension2 = webSite.GetSiteExtension();
                var deploy2_lro = await extension2.CreateOrUpdateAsync(WaitUntil.Completed, new WebAppMSDeploy()
                {
                    PackageUri = new Uri("https://github.com/Azure/azure-libraries-for-net/raw/master/Samples/Asset/square-function-app.zip"),
                });
                var deploy2 = deploy2_lro.Value;

                // warm up
                Utilities.Log("Warming up " + app5Url + "/api/square...");
                Utilities.PostAddress("http://" + app5Url + "/api/square", "925");
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app5Url + "/api/square...");
                Utilities.Log("Square of 925 is " + Utilities.PostAddress("http://" + app5Url + "/api/square", "925"));
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    resourceGroup.Delete(Azure.WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}