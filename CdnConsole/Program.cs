using System;
using System.Collections.Generic;
using Microsoft.Azure;
using Microsoft.Azure.Management.Cdn;
using Microsoft.Azure.Management.Cdn.Models;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

namespace CdnConsole
{
    class Program
    {
        //Tenant app constants
        private const string clientID = "cb334d9a-000e-44c4-b22c-caf610c2e140";
        private const string redirectUri = "http://camthegeek.com/cdnconsolealpha";
        private const string authority = "https://login.microsoftonline.com/084e6b0e-7c64-4bf4-8df3-62db51236fe2";

        //Subscription constants
        private const string subscriptionId = "376c977e-dcb5-4959-a52f-7d6999058559";

        //Application constants
        private const string profileName = "CdnConsoleApp";
        private const string endpointName = "CdnConsoleEndpoint";
        private const string resourceGroupName = "CdnConsoleRG";
        private const string resourceLocation = "Central US";
        private const string originName = "Contoso origin";
        private const string originHostName = "www.contoso.com";

        static void Main(string[] args)
        {
            // Authenticate User with ADAL
            AuthenticationContext authContext = new AuthenticationContext(authority);
            AuthenticationResult authResult = 
                authContext.AcquireTokenAsync("https://management.core.windows.net/", 
                                                clientID, 
                                                new Uri(redirectUri), 
                                                new PlatformParameters(PromptBehavior.RefreshSession)).Result;

            // Create Resource Group (if needed)
            var arm = new ResourceManagementClient(new TokenCloudCredentials(subscriptionId, authResult.AccessToken));
            CreateResourceGroup(arm);

            // Create CDN client
            CdnManagementClient cdn = new CdnManagementClient(new TokenCredentials(authResult.AccessToken))
            { SubscriptionId = subscriptionId };

            // We'll use these to indicate if the profile and/or endpoint already exist
            bool profileAlreadyExists = false;
            bool endpointAlreadyExists = false;

            // List all the CDN profiles on this subscription
            var profileList = cdn.Profiles.ListBySubscriptionId();
            foreach (Profile p in profileList)
            {
                Console.WriteLine("CDN profile {0} in Resource Group {1}", p.Name, GetResourceGroupNameFromProfile(p));
                if (p.Name == profileName)
                {
                    // Hey, that's the name of the CDN profile we want to create!
                    profileAlreadyExists = true;
                }

                //List all the CDN endpoints on this CDN profile
                Console.WriteLine("Endpoints:");
                var endpointList = cdn.Endpoints.ListByProfile(p.Name, GetResourceGroupNameFromProfile(p));
                foreach (Endpoint e in endpointList)
                {
                    Console.WriteLine("-{0} ({1})", e.Name, e.HostName);
                    if (e.Name == endpointName)
                    {
                        // The unique endpoint name already exists.
                        endpointAlreadyExists = true;
                    }
                }
                Console.WriteLine("");
            }

            // Create CDN Profile
            if (profileAlreadyExists)
            {
                Console.WriteLine("Profile {0} already exists.", profileName);
            }
            else
            {
                CreateCdnProfile(cdn);
            }

            // Create CDN Endpoint
            if (endpointAlreadyExists)
            {
                Console.WriteLine("Endpoint {0} already exists.", endpointName, profileName);
            }
            else
            {
                CreateCdnEndpoint(cdn);
            }

            Console.WriteLine("");

            // Purge CDN Endpoint
            PromptPurgeCdnEndpoint(cdn);

            // Delete CDN Endpoint
            PromptDeleteCdnEndpoint(cdn);

            // Delete CDN Profile
            PromptDeleteCdnProfile(cdn);

            //Delete Resource Group
            PromptDeleteResourceGroup(arm);

            Console.WriteLine("Press Enter to end program.");
            Console.ReadLine();
        }

        /// <summary>
        /// Prompts the user to delete the resource group, then carries it out.
        /// </summary>
        /// <param name="arm">An authenticated ResourceManagementClient</param>
        private static void PromptDeleteResourceGroup(ResourceManagementClient arm)
        {
            if(PromptUser(String.Format("Delete Resource Group {0}?", resourceGroupName)))
            {
                Console.WriteLine("Deleting Resource Group. Please wait...");
                arm.ResourceGroups.Delete(resourceGroupName);
                Console.WriteLine("Done.");
                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Prompts the user to delete the CDN profile, then carries it out.
        /// </summary>
        /// <param name="cdn">An authenticated CdnManagementClient</param>
        private static void PromptDeleteCdnProfile(CdnManagementClient cdn)
        {
            if(PromptUser(String.Format("Delete CDN profile {0}?", profileName)))
            {
                Console.WriteLine("Deleting profile. Please wait...");
                cdn.Profiles.DeleteIfExists(profileName, resourceGroupName);
                Console.WriteLine("Done.");
                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Prompts the user to delete the CDN endpoint, then carries it out.
        /// </summary>
        /// <param name="cdn">An authenticated CdnManagementClient</param>
        private static void PromptDeleteCdnEndpoint(CdnManagementClient cdn)
        {
            if(PromptUser(String.Format("Delete CDN endpoint {0} on profile {1}?", endpointName, profileName)))
            {
                Console.WriteLine("Deleting endpoint. Please wait...");
                cdn.Endpoints.DeleteIfExists(endpointName, profileName, resourceGroupName);
                Console.WriteLine("Done.");
                Console.WriteLine("");
            }
            
        }

        /// <summary>
        /// Creates the Resource Group if it doesn't already exist.
        /// </summary>
        /// <param name="arm">An authenticated ResourceManagementClient</param>
        private static void CreateResourceGroup(ResourceManagementClient arm)
        {
            if (arm.ResourceGroups.CheckExistence(resourceGroupName).Exists)
            {
                Console.WriteLine("Resource group {0} already exists.", resourceGroupName);
            }
            else
            {
                Console.WriteLine("Creating resource group {0}.", resourceGroupName);
                arm.ResourceGroups.CreateOrUpdate(resourceGroupName, new ResourceGroup(resourceLocation));
            }
        }

        /// <summary>
        /// Creates a CDN profile.
        /// </summary>
        /// <param name="cdn">An authenticated CdnManagementClient</param>
        private static void CreateCdnProfile(CdnManagementClient cdn)
        {
            Console.WriteLine("Creating profile {0}.", profileName);
            ProfileCreateParameters profileParms =
                new ProfileCreateParameters() { Location = resourceLocation, Sku = new Sku(SkuName.StandardAkamai) };
            cdn.Profiles.Create(profileName, profileParms, resourceGroupName);
        }

        /// <summary>
        /// Purges the CDN endpoint.
        /// </summary>
        /// <param name="cdn">An authenticated CdnManagementClient</param>
        private static void PromptPurgeCdnEndpoint(CdnManagementClient cdn)
        {
            if(PromptUser(String.Format("Purge CDN endpoint {0}?", endpointName)))
            {
                Console.WriteLine("Purging endpoint. Please wait...");
                cdn.Endpoints.PurgeContent(endpointName, profileName, resourceGroupName, new List<string>() { "/*" });
                Console.WriteLine("Done.");
                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Creates the CDN endpoint.
        /// </summary>
        /// <param name="cdn">An authenticated CdnManagementClient</param>
        private static void CreateCdnEndpoint(CdnManagementClient cdn)
        {
            Console.WriteLine("Creating endpoint {0} on profile {1}.", endpointName, profileName);
            EndpointCreateParameters endpointParms =
                new EndpointCreateParameters()
                {
                    Origins = new List<DeepCreatedOrigin>() { new DeepCreatedOrigin(originName, originHostName) },
                    IsHttpAllowed = true,
                    IsHttpsAllowed = true,
                    Location = resourceLocation
                };
            cdn.Endpoints.Create(endpointName, endpointParms, profileName, resourceGroupName);
        }

        /// <summary>
        /// Parses the ID of a CDN profile to extract the name of the resource group to which it belongs.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns>A resource group name</returns>
        private static string GetResourceGroupNameFromProfile(Profile profile)
        {
            return profile.Id.Split('/')[4];
        }

        /// <summary>
        /// Prompts the user with a yes/no question and returns the result.
        /// </summary>
        /// <param name="Question">The question to ask the user.</param>
        /// <returns>A boolean if the user's response.</returns>
        private static bool PromptUser(string Question)
        {
            Console.Write(Question + " (Y/N): ");
            var response = Console.ReadKey();
            Console.WriteLine();
            if (response.Key == ConsoleKey.Y)
            {
                return true;
            }
            else if (response.Key == ConsoleKey.N)
            {
                return false;
            }
            else
            {
                // They're not pressing Y or N.  Let's ask them again.
                return PromptUser(Question);
            }
        }

    }
}
