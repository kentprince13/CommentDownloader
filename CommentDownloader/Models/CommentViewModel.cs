using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommentDownloader.Models
{
    public class CommentViewModel
    {
        public string  UserName { get; set; }
        public string DateTime { get; set; }
        public string StarRating { get; set; }
        public string Comment { get; set; }
        public string Link { get; set; }
    }
}