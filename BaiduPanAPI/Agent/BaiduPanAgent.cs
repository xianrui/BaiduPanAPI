﻿using BaiduPanApi.Agent.Model;
using Gaia.Common.Collections;
using Gaia.Common.Execute.Control;
using Gaia.Common.Net.Http;
using Gaia.Common.Net.Http.RequestModifier;
using Gaia.Common.Net.Http.ResponseParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BaiduPanApi.Agent
{
    public class BaiduPanAgent
    {
        
        public string BDUSS { get; set; }
        public string BDSToken { get; set; }
        public string EncodedBDUSS { get; set; }
        public BaiduPanAgent(string bduss)
        {
            BDUSS = bduss;
        }

        public string RefreshToken()
        {
            WebClient client = new WebClient();
            client.Headers.Set("Cookie", "BDUSS=" + BDUSS);
            string content = UTF8Encoding.UTF8.GetString(client.DownloadData("http://pan.baidu.com/disk/home"));
            BDSToken = ExtractString(content, "yunData.MYBDSTOKEN = \"", "\";");
            //EncodedBDUSS = ExtractString(content, "yunData.MYBDUSS = \"", "\";");
            return BDSToken;
        }

        private string ExtractString(string content, string beginMatch, string endMatch){
            var beginIndex = content.IndexOf(beginMatch) + beginMatch.Length;
            if (beginIndex == -1) return null;
            var endIndex = content.IndexOf(endMatch, beginIndex);
            if (endIndex == -1) return null;
            int length = endIndex - beginIndex;
            return content.Substring(beginIndex, length);
        }

        public PCSUploadResponse UploadTempFile(FileStream stream)
        {
            PCSUploadResponse result = HttpHelper.SendRequest(new Uri("https://c.pcs.baidu.com/rest/2.0/pcs/file?method=upload&type=tmpfile&app_id=250528"), 
                HttpMethod.POST, 
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS),
                    new HttpRequestMultipartFormModifier(null, new KeyValuePairList<string, HttpRequestMultipartFormModifier.FileInfo>(){
                        { "Filedata", new HttpRequestMultipartFormModifier.FileInfo(stream) }
                    })
                },
                new HttpResponseJSONObjectParser<PCSUploadResponse>(),
                null);
            return result;
        }

        public PanCreateResponse CreateCloudFile(string path, long size, string md5)
        {
            KeyValuePairList<string, string> parameters = new KeyValuePairList<string, string>(){
                { "path", path },
                { "isdir", "0" },
                { "size", size.ToString() },
                { "block_list", "[\"" + md5 + "\"]" }
            };

            PanCreateResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/api/create?a=commit&channel=chunlei&clienttype=0&web=1"),
                HttpMethod.POST,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("bdstoken", BDSToken),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS),
                    new HttpRequestUrlEncodedFormModifier(parameters)
                },
                new HttpResponseJSONObjectParser<PanCreateResponse>(),
                null);
            return result;
        }

        public PanSearchResponse Search(string keyword, int page, int num)
        {
            PanSearchResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/api/search?order=time&desc=1&showempty=0&recursion=1&app_id=250528"),
                HttpMethod.GET,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("bdstoken", BDSToken),
                    new HttpRequestSimpleUriModifier("key", keyword),
                    new HttpRequestSimpleUriModifier("page", page.ToString()),
                    new HttpRequestSimpleUriModifier("num", num.ToString()),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS)
                },
                new HttpResponseJSONObjectParser<PanSearchResponse>(),
                null);
            return result;
        }

        public PanShareResponse Share(long fileId)
        {
            KeyValuePairList<string, string> parameters = new KeyValuePairList<string, string>(){
                { "fid_list", "[" + fileId + "]" },
                { "schannel", "0" },
                { "channel_list", "[]" }
            };

            PanShareResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/share/set?app_id=250528"),
                HttpMethod.POST,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("bdstoken", BDSToken),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS),
                    new HttpRequestUrlEncodedFormModifier(parameters)
                },
                new HttpResponseJSONObjectParser<PanShareResponse>(),
                null);
            return result;
        }

        public PanTorrentQueryResponse QueryTorrentInfo(string sourcePath)
        {
            PanTorrentQueryResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/rest/2.0/services/cloud_dl?method=query_sinfo&type=2&app_id=250528"),
                HttpMethod.GET,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("bdstoken", BDSToken),
                    new HttpRequestSimpleUriModifier("source_path", sourcePath),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS)
                },
                new HttpResponseJSONObjectParser<PanTorrentQueryResponse>(),
                null);
            return result;
        }

        public PanOfflineDownloadResponse OfflineDownload(string sourcePath, string fileSHA1, string savePath, int[] selectedIndexes)
        {
            string strSelectedIndexes = String.Join(",", selectedIndexes);
            PanOfflineDownloadResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/rest/2.0/services/cloud_dl?app_id=250528"),
                HttpMethod.POST,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("bdstoken", BDSToken),
                    new HttpRequestUrlEncodedFormModifier(new KeyValuePairList<string, string>(){
                        { "method", "add_task" },
                        { "file_sha1", fileSHA1 },
                        { "save_path", savePath },
                        { "selected_idx", strSelectedIndexes },
                        { "task_from", "2" },
                        { "source_path", sourcePath },
                        { "type", "2" }
                    }),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS)
                },
                new HttpResponseJSONObjectParser<PanOfflineDownloadResponse>(),
                null);
            return result;
        }

        public PanTaskQueryResponse QueryOfflineTasks(int start, int limit)
        {
            return QueryOfflineTasks(start, limit, 255);
        }

        public PanTaskQueryResponse QueryOfflineTasks(int start, int limit, int statusFlag)
        {
            PanTaskQueryResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/rest/2.0/services/cloud_dl?method=list_task&need_task_info=1&app_id=250528"),
                HttpMethod.GET,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("bdstoken", BDSToken),
                    new HttpRequestSimpleUriModifier("start", start.ToString()),
                    new HttpRequestSimpleUriModifier("limit", limit.ToString()),
                    new HttpRequestSimpleUriModifier("status", statusFlag.ToString()),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS)
                },
                new HttpResponseJSONObjectParser<PanTaskQueryResponse>(),
                null);
            return result;
        }

        public PanShareDownloadResponse GetDirectDownloadLink(string shareUrl)
        {
            WebClient client = new WebClient();
            string content = UTF8Encoding.UTF8.GetString(client.DownloadData(shareUrl));
            var sign = ExtractString(content, "yunData.SIGN = \"", "\";");
            var uk = ExtractString(content, "yunData.SHARE_UK = \"", "\";");
            var shareId = ExtractString(content, "yunData.SHARE_ID = \"", "\";");
            var fileId = ExtractString(content, "\"fs_id\":", ",");
            var timestamp = ExtractString(content, "yunData.TIMESTAMP = \"", "\";");
            PanShareDownloadResponse result = HttpHelper.SendRequest(new Uri("http://pan.baidu.com/api/sharedownload?app_id=250528"),
                HttpMethod.POST,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleUriModifier("timestamp", timestamp),
                    new HttpRequestSimpleUriModifier("sign", sign),
                    new HttpRequestUrlEncodedFormModifier(new KeyValuePairList<string, string>(){
                        { "sign", sign },
                        { "encrypt", "0" },
                        { "product", "share" },
                        { "uk", uk },
                        { "primaryid", shareId },
                        { "timestamp", timestamp },
                        { "fid_list", "[" + fileId + "]" }
                    }),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS)
                },
                new HttpResponseJSONObjectParser<PanShareDownloadResponse>(),
                null);
            return result;
        }


        public HttpResponseFileParser.TempFileInfo Download(string fileUrl, IExecutionControl control)
        {
            var result = HttpHelper.SendRequest(new Uri(fileUrl),
                HttpMethod.GET,
                new List<IHttpRequestModifier>(){
                    new HttpRequestSimpleHeaderModifier("User-Agent", "netdisk;4.4.0.6;PC;PC-Windows;6.2.9200;WindowsBaiduYunGuanJia"),
                    new HttpRequestSimpleHeaderModifier("Cookie", "BDUSS=" + BDUSS),
                    new HttpRequestSimpleHeaderModifier("Refer", "http://pan.baidu.com/disk/home")
                },
                new HttpResponseFileParser(),
                control);
            return result;
        }
    }
}
