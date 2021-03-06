﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Runtime.Serialization;
using Web.Admin.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Web.Admin.Data;
using Tweetinvi;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Web.Admin.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string API_KEY;
        private readonly TweetResponse Settings;
        private readonly IMapPointRepository db;

        public MapController(IHostingEnvironment host, IConfiguration configuration) {

            _configuration = configuration;

            API_KEY = _configuration.GetValue<string>("IncomingMapAPIKey");

            var dataFolderPath = host.ContentRootPath + @"/_datastore/";
            Settings = SettingsRepository.GetSettings(dataFolderPath);
            db = new SqLiteMapPointRepository(dataFolderPath);
        }

        // GET: api/values
        [HttpGet]
        public JsonResult Get(bool? getall)
        {
            List<MapPoint> mapData = db.GetAll(getall.HasValue && getall.Value);
            return Json(mapData);
        }
        [HttpGet("{id}")]
        public JsonResult Get(int id)
        {
            MapPoint point = db.Get(id);
            return Json(point);
        }


        // POST api/values
        /// <summary>
        /// Save new Tweet to be shown on map
        /// </summary>
        /// <param name="value"></param>
        /// <returns>JSON that includes if reply tweet should be sent and if so what the text should be</returns>
        [HttpPost]
        public JsonResult Post([FromBody]MapPoint value)
        {
            if (!AuthHeaderIsValid())
            {
                return Json(new object[] { "Access denied" });
            }
            //todo: validation?

            if (!String.IsNullOrEmpty(value.TweetId))
            {
                db.AddNew(value); // assume we have all tweet data
            }
            else if (!String.IsNullOrEmpty(value.TweetUrl))
            {

                //assume url in form of https://twitter.com/LGBTYS/status/1135926878202748930
                var tweetLongID = Convert.ToInt64(value.TweetUrl.Split('/').Last());

                //get the details from TweetURL and save new one

                Auth.SetUserCredentials(_configuration.GetValue<string>("TwitterCredentials:ConsumerKey"),
                    _configuration.GetValue<string>("TwitterCredentials:ConsumerSecret"),
                    _configuration.GetValue<string>("TwitterCredentials:UserAccessToken"),
                    _configuration.GetValue<string>("TwitterCredentials:UserAccessSecret"));
                var tweet = Tweet.GetTweet(tweetLongID);
                if (tweet != null)
                {
                    MapPoint newTweet = new MapPoint
                    {
                        Hide = value.Hide,
                        TweetUrl = tweet.Url,
                        TweetId = tweet.IdStr,
                        Text = tweet.Text,
                        Area = value.Area,
                        TwitterHandle = tweet.CreatedBy.ScreenName,
                        CreatedDate = tweet.CreatedAt,
                        LocationConfidence = "High",
                        Img = tweet.Media.First(x => x.MediaType != "video").MediaURLHttps,
                        Geo = value.Geo

                    };
                    db.AddNew(newTweet);
                }
                else
                {
                    throw new ApplicationException("Unable to find that tweet!");
                }
            }
            else
            {
                throw new ApplicationException("Unable to add tweet!");
            }

            return Json(Settings);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(string id, [FromBody]MapPoint point)
        {
            if (!AuthHeaderIsValid())
            {
                return;
            }

            db.Update(point);
        }

        // DELETE api/values/5
        /*[HttpDelete("{id}")]
        public void Delete(int id)
        {
        }*/


        private bool AuthHeaderIsValid()
        {
            var authHeader = (string)Request.Headers["Authorization"];

            if (string.IsNullOrEmpty(API_KEY) || authHeader == null || authHeader != API_KEY)
            {
                return false;
            }

            return true;
        }
    }
}
