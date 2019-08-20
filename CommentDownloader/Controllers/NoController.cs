using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CommentDownloader.Controllers
{
    public class NoController : ApiController
    {
        [HttpGet]
        public string Err()
        {
            var j = new CommentController();
           var y = j.doIt();
            
            return y;
        }
    }
}
