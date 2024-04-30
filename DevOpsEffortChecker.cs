using System;
using System.Collections.Generic;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace DevOpsHoursChecker
{
    class Program
    {
        //This is a URL and a Personal Access Token to a fake project I set up on a junk DevOps account. No security risk here.
        internal const string azureDevOpsOrganizationUrl = "https://dev.azure.com/fungusanimalclark/";
        internal const string pat = "6xg3muxrgynqygp2m7b3hdar7llsoywotglupsid5d55xxcuyria";
        internal const string projectName = "FakeProject";
        static void Main(string[] args)
        {
            var parentlist = new List<string>();
            var connection = new VssConnection(new Uri(azureDevOpsOrganizationUrl), new VssBasicCredential(string.Empty, pat));
            var wiclient = connection.GetClient<WorkItemTrackingHttpClient>();
            VssBasicCredential credentials = new("", pat);
            Wiql wiql = new()
            {
                Query = "Select * " +
                "From WorkItems " +
                "Where [System.TeamProject] = '" + projectName + "' " +
                "And [System.State] <> 'Closed' " +
                "And [System.RelatedLinkCount] > '0'" +
                "Order By [State] Asc, [Changed Date] Desc"
            };

            WorkItemQueryResult workItemQueryResult = wiclient.QueryByWiqlAsync(wiql).Result;

            //I got so excited at this point once I finally had a query result I could work with! I was on the home stretch!
            
            //Finds Number of Largest Id
            int largestID = 0;

            foreach (var item in workItemQueryResult.WorkItems)
            {

                var wi = wiclient.GetWorkItemAsync(id: Convert.ToInt32(item.Id), expand: WorkItemExpand.All).Result;

                foreach (var field in wi.Fields)
                {
                    int? ID = wi.Id;
                    if (ID > largestID)
                    { largestID = (int)ID; }
                }
            }

            //Creates Array With one List Item for each Work Item
            int[] sumsOfHours = new int[largestID];

            //Sums hours of all children
            foreach (var item in workItemQueryResult.WorkItems)
            {
                var wi = wiclient.GetWorkItemAsync(id: Convert.ToInt32(item.Id), expand: WorkItemExpand.All).Result;

                int parentID = 0;
                int originalHours = 0;
                foreach (var field in wi.Fields)
                {
                    if (field.Key == "System.Parent")
                    {
                        parentID = Convert.ToInt32(field.Value);
                    }

                    if (field.Key == "Microsoft.VSTS.Scheduling.OriginalEstimate")
                    {
                        originalHours = Convert.ToInt32(field.Value);
                    }
                }
                if (parentID != 0)
                {
                    sumsOfHours[parentID - 1] = originalHours;
                }
            }

            int Id = 0;
            int Hours = 0;

            //Returns Mismatches
            foreach (var item in workItemQueryResult.WorkItems)
            {
                var wi = wiclient.GetWorkItemAsync(id: Convert.ToInt32(item.Id), expand: WorkItemExpand.All).Result;

                foreach (var field in wi.Fields)
                {
                    if (field.Key == "System.Id")
                    {
                        Id = Convert.ToInt32(field.Value);
                    }

                    if (field.Key == "Microsoft.VSTS.Scheduling.OriginalEstimate")
                    {
                        Hours = Convert.ToInt32(field.Value);
                    }
                }

                if (Hours != sumsOfHours[Id - 1] && sumsOfHours[Id - 1] != 0)
                {
                    Console.WriteLine($"Work Item #{Id} had {Hours - sumsOfHours[Id - 1]} more hours allotted to it than the summation of it's children.");
                }
            }
        }
    }
}