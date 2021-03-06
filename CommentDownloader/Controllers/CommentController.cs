﻿using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using CommentDownloader.Models;
using HtmlAgilityPack;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json.Linq;
using WebGrease.Activities;

namespace CommentDownloader.Controllers
{
    public class CommentController : Controller
    {

        public string doIt()
        {
            var mapPath = Server.MapPath(Path.Combine("~/new/", "new234.csv"));
            var filepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"/new/new234.csv");

            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"/new234.csv";
            return $@"{path} : {filepath} : {mapPath}";

        }

        public ActionResult Index()
        {
            return View();
        }

        private const string YoutubeLinkRegex = "(?:.+?)?(?:\\/v\\/|watch\\/|\\?v=|\\&v=|youtu\\.be\\/|\\/v=|^youtu\\.be\\/)([a-zA-Z0-9_-]{11})+";
        private static Regex regexExtractId = new Regex(YoutubeLinkRegex, RegexOptions.Compiled);
        private static string[] validAuthorities = { "youtube.com", "www.youtube.com", "youtu.be", "www.youtu.be" };

        public string ExtractVideoIdFromUri(Uri uri)
        {
            try
            {

                string authority = new UriBuilder(uri).Uri.Authority.ToLower();

                if (validAuthorities.Contains(authority))
                {
                    var regRes = regexExtractId.Match(uri.ToString());
                    if (regRes.Success)
                    {
                        return regRes.Groups[1].Value;
                    }
                }
            }
            catch
            {
                InputViewModel.ErrorMessage = "sorry invalid links";
               
            }


            return null;
        }



        [HttpPost]
        public ActionResult Comment(InputViewModel viewModel)
        {
            
            try
            {
                var videoUrl = viewModel.InputUrl;
                if (videoUrl.IsNullOrWhiteSpace())
                {InputViewModel.ErrorMessage= " Sorry Invalid Link";
                    return View("Err");
                }
                   

                var firstUrlFormatId = "";
                var otherUrlFormatId = "";

                if (videoUrl.Contains("youtube.com/watch?v="))
                {
                    firstUrlFormatId = videoUrl;
                }
                else
                {
                    otherUrlFormatId = ExtractVideoIdFromUri(new Uri(videoUrl, UriKind.Absolute));
                }
                var urlStrings = firstUrlFormatId.Split('=');

                var videoId = (firstUrlFormatId.IsNullOrWhiteSpace()) ? otherUrlFormatId : urlStrings[1];

                var youTubeUrl = $"/youtube/v3/commentThreads?part=snippet&maxResults=100&order=time&videoId={videoId}&key=AIzaSyDcXc9agwa4geoXqMmHj2gTea10BvxvX_0";
                YouTubeComments(viewModel, youTubeUrl, videoId);

                return View("Notify", viewModel);
            }
            catch
            {


                InputViewModel.ErrorMessage = "error0000";
                return View("Err");
               
            }
           
        }



        private void YouTubeComments(InputViewModel viewModel, string youTubeUrl, string videoId)
        {
            using (var client = new HttpClient())
            {
                var commentList = new List<CommentViewModel>();
                client.BaseAddress = new Uri("https://www.googleapis.com");
                var responseTask = client.GetAsync(youTubeUrl);
                responseTask.Wait();
                var result = responseTask.Result;
                var stringAsync = result.Content.ReadAsStringAsync().Result;

                //var convertToJson = JsonConvert.DeserializeObject(stringAsync);
                var convertToJson = JObject.Parse(stringAsync);
                var items = convertToJson["items"];

                if (!result.IsSuccessStatusCode || !items.Any())
                {
                    viewModel.CommentLength = 0;
                }
                else
                {
                    ProcessMoreYouTubeComments(viewModel, items, commentList, convertToJson, videoId, client);
                }
            }
        }



        private void ProcessMoreYouTubeComments(InputViewModel viewModel, JToken items, List<CommentViewModel> commentList,
            JObject convertToJson, string videoId, HttpClient client)
        {
            AddComment(items, commentList);

            var pageToken = (string)convertToJson["nextPageToken"];

            while (pageToken != null)
            {
                var nextPageToken = pageToken;

                var newCommentUrl =
                    $"/youtube/v3/commentThreads?part=snippet&maxResults=100&order=time&pageToken={nextPageToken}&videoId={videoId}&key=AIzaSyDcXc9agwa4geoXqMmHj2gTea10BvxvX_0";

                var newTask = client.GetAsync(newCommentUrl);
                newTask.Wait();

                var newResult = newTask.Result.Content.ReadAsStringAsync().Result;

                var newConvertToJson = JObject.Parse(newResult);

                var newItems = newConvertToJson["items"];

                pageToken = string.Empty;

                AddComment(newItems, commentList);

                var newPageToken = (string) newConvertToJson["nextPageToken"];
                pageToken = newPageToken;
                if (commentList.Count == 100000)
                {
                    break;
                }

                if (pageToken == null)  
                    break;
             
            }


            ConvertToCsv(commentList);
            viewModel.CommentLength = commentList.Count;
        }



        private  void AddComment(JToken items, List<CommentViewModel> commentList)
        {
            try
            {
                
                foreach (var item in items)
                {
                    

                    var vm = new CommentViewModel
                    {
                        UserName = (string)item["snippet"]["topLevelComment"]["snippet"]["authorDisplayName"],
                        DateTime = DateTime
                            .Parse((string)item["snippet"]["topLevelComment"]["snippet"]["publishedAt"])
                            .ToString("yyyy MMMM dd"),
                        Comment = (string)item["snippet"]["topLevelComment"]["snippet"]["textOriginal"],
                        StarRating = (string)item["snippet"]["topLevelComment"]["snippet"]["viewerRating"],
                        Link = (string)item["snippet"]["topLevelComment"]["snippet"]["authorChannelUrl"]
                    };
                    commentList.Add(vm);
                 
                }
                
            }
            catch (Exception e)
            {
                InputViewModel.ErrorMessage = "sorry, error occur while loading comments";

            }
        }



        private void ConvertToCsv(IEnumerable<CommentViewModel> commentList)
        {
            try
            {
                   // System.IO.File.Create(filepath).Close();

                var csv = new StringBuilder();
                csv.AppendLine($"{"Name"},{"Date"},{"Star Rating"},{"Comment"},{"Link"}");

                foreach (var item in commentList)
                {
                    var username = item.UserName;
                    var date = item.DateTime;
                    var starRating = (item.StarRating.Contains("none")) ? " " : item.StarRating;
                    var comment = item.Comment;
                    var link = (item.Link.Contains("none")) ? " " : item.Link;

                    var newLine = $"{username},{date},{starRating},{comment},{link}";
                    var formatted = RemoveLineEndings(newLine);
                    csv.AppendLine(formatted);
                }

               
                var mapPath = Server.MapPath(Path.Combine("~/new/","new234.csv"));
                var filepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"/Users/new/new234.csv");

                var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"/new234.csv"; 
                
                //System.IO.File.Open(mapPath, FileMode.OpenOrCreate, FileAccess.ReadWrite).Close();
              //  FileIOPermission(FileIOPermissionAccess.Write, @"/new/new234.csv");

                 System.IO.File.AppendAllText(path, csv.ToString());
            }
            catch (Exception e)
            {
                InputViewModel.ErrorMessage = "Error occur while saving comments.";
                InputViewModel.ErrorMessage += e.Message;
            }
           
        }


        
        public  string RemoveLineEndings(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            var lineSeparator = ((char)0x2028).ToString();
            var paragraphSeparator = ((char)0x2029).ToString();

            return value.Replace("\r\n", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\r", string.Empty)
                .Replace(lineSeparator, string.Empty)
                .Replace(paragraphSeparator, string.Empty);
        }








        public ActionResult CheckAction(InputViewModel viewModel)
        {

            try
            {
                FileSecurity sec = System.IO.File.GetAccessControl(@"d:\new");
                AuthorizationRuleCollection rules = sec.GetAccessRules(true, true,
                    typeof(NTAccount));
                foreach (FileSystemAccessRule rule in rules)
                {
                    InputViewModel.ErrorMessage = rule.AccessControlType.ToString(); // Allow or Deny
                    InputViewModel.ErrorMessage = rule.FileSystemRights.ToString(); // e.g., FullControl
                    InputViewModel.ErrorMessage = rule.IdentityReference.Value.ToString(); // e.g., MyDomain/Joe
                }
                var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                string usersAccount = sid.Translate(typeof(NTAccount)).ToString();
                FileSystemAccessRule newRule = new FileSystemAccessRule
                    (usersAccount, FileSystemRights.ExecuteFile, AccessControlType.Allow);
                sec.AddAccessRule(newRule);
                System.IO.File.SetAccessControl(@"d:\new", sec);



            }
            catch (Exception e)
            {
                InputViewModel.ErrorMessage = "Another Error occur while playing ";
                InputViewModel.ErrorMessage += e.Message;
            }
            return View("Notify", viewModel);
        }










        public ActionResult AmazonReview()
        {
            var agile = new HtmlWeb()
            {
                PreRequest = request =>
                {
                    request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                    return true;
                }
            };

            var doc = agile.Load(
                "https://www.amazon.com/eero-Home-WiFi-System-Pack/product-reviews/B00XEW3YD6/ref=cm_cr_dp_d_show_all_btm?ie=UTF8&reviewerType=all_reviews");
            var name = doc.DocumentNode.SelectNodes("  //*[@id=\"cm_cr-review_list\"]");
            var nay = name.Nodes();
           foreach (var item in name.Nodes())
            {
                
                var you = item.SelectSingleNode("/html[1]/body[1]/div[1]/div[2]/div[1]/div[2]/div[1]/div[1]/div[4]/div[3]");
                
                
                foreach (var element in you.ChildNodes)
                {
                    var t = element.SelectSingleNode("div[1]/div[1]/div[4]").InnerText;
                    var x = element.SelectSingleNode("div[1]/div[1]/div[2]").InnerText;
                    break;

                }
               // "/html[1]/body[1]/div[1]/div[2]/div[1]/div[2]/div[1]/div[1]/div[4]/div[3]/div[7]/div[1]/div[1]/div[4]"
               // var l = item.SelectNodes("/html[1]/body[1]/div[1]/div[2]/div[1]/div[2]/div[1]/div[1]/div[4]/div[3]/div[2]c");
                //"/html[1]/body[1]/div[1]/div[2]/div[1]/div[2]/div[1]/div[1]/div[4]/div[3]/div[9]/div[1]/div[1]/div[4]";
              // var h = l;
            }

            return View("Index");

            //https://www.amazon.com/eero-Home-WiFi-System-Pack/dp/B00XEW3YD6/ref=sr_1_1?s=pc&ie=UTF8&qid=148925
            // 0467&sr=1-1&keywords=eero&th=1#customerReviews

            //https://www.amazon.com/eero-Home-WiFi-System-Pack/product-reviews/B00XEW3YD6/ref=cm_cr_getr_d_paging_btm_next_3?ie=UTF8&reviewerType=all_reviews&pageNumber=170


            //amazon.com/Apple-iPhone-Plus-Unlocked-64GB/dp/B0775FLHPN/ref=br_asw_pdt-3?pf_rd_m=ATVPDKIKX0DER&pf_rd_s=&pf_rd_r=V2P4X0W9BXMTXDNT3F55&pf_rd_t=36701&pf_rd_p=74c2af8b-5acb-4bf8-b252-8b1584c94b14&pf_rd_i=desktop

            // https://www.amazon.com/Apple-iPhone-Plus-Unlocked-64GB/product-reviews/B0775FLHPN/ref=cm_cr_arp_d_paging_btm_next_2?ie=UTF8&reviewerType=all_reviews&pageNumber=2




            //Total review count
            ////*[@id="dp-summary-see-all-reviews"]/h2
            /// //*[@id="cm_cr-product_info"]/div/div[1]/div[2]/div/div/div[2]/div/span


            // show all product button and link review count
            //*[@id="reviews-medley-footer"]/div[2]/a
            // "/eero-Home-WiFi-System-Pack/product-reviews/B00XEW3YD6/ref=cm_cr_dp_d_show_all_btm?ie=UTF8&amp;reviewerType=all_reviews"

            // next button
            //*[@id="cm_cr-pagination_bar"]/ul/li[2]/a
            //<a href="/eero-Home-WiFi-System-Pack/product-reviews/B00XEW3YD6/ref=cm_cr_arp_d_paging_btm_2?ie=UTF8&amp;pageNumber=2&amp;reviewerType=all_reviews">Next page<span class="a-letter-space"></span><span class="a-letter-space"></span>→</a>

            //*[@id="a-page"]/div[2]/div[1]/div[2]/div/div[1]/div[4]
            //*[@id="cm_cr-review_list"]
            //    //*[@id="lid-547390014"]/div
            //    //*[@id="lid-547390014"]/div/div/div[2]/div[3]/p/span/text()

            //    //name //*[@id="customer_review-R3AB0WSPR3PUT7"]/div[1]/a/div[2]/span
            //    // rating //*[@id="customer_review-R3AB0WSPR3PUT7"]/div[2]/a[1]/i/span
            //    // date//*[@id="customer_review-R3AB0WSPR3PUT7"]/span.innertxt
            //    //comment //*[@id="customer_review-R3AB0WSPR3PUT7"]/div[4]/span/span/text()[1]  //*[@id="customer_review-R3AB0WSPR3PUT7"]/div[4]/span/span
            //    //link //*[@id="customer_review-R1LN2K8771FZ7B"]/div[1]/div[1]/div/a
        }




    }
}
