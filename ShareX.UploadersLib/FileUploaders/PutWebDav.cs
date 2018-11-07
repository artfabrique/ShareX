#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2018 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShareX.HelpersLib;
using ShareX.UploadersLib.Properties;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShareX.UploadersLib.FileUploaders
{
    public class PutWebDavFileUploaderService : FileUploaderService
    {
        public override FileDestination EnumValue { get; } = FileDestination.PutWebDav;

        public override Image ServiceImage => Resources.PutWebDav;

        public override bool CheckConfig(UploadersConfig config)
        {
            return !string.IsNullOrEmpty(config.PutWebDavHost);
        }

        public override GenericUploader CreateUploader(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            return new PutWebDav(config.PutWebDavHost, config.PutWebDavPort, config.PutWebDavUsername, config.PutWebDavPassword, config.PutWebDavDirectory, config.PutWebDavBaseURL)
            {
                Host = config.PutWebDavHost,
                Port = config.PutWebDavPort.ToString(),
                Username = config.PutWebDavUsername,
                Password = config.PutWebDavPassword,
                Directory = config.PutWebDavDirectory,
                BaseURL = config.PutWebDavBaseURL,
                Filename = ""
            };
        }

        public override TabPage GetUploadersConfigTabPage(UploadersConfigForm form) => form.tpOwnCloud;
    }

    public sealed class PutWebDav : FileUploader
    {
        public string Host { get; set; }
        public string Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Directory { get; set; }
        public string BaseURL { get; set; }

        public bool CreateShare { get; set; }

        public string Filename { get; set; }


        public PutWebDav(string host, int port, string username, string password, string directory, string baseUrl)
        {
            Host = host;
            Port = port.ToString();
            Username = username;
            Password = password;
            Directory = directory;
            BaseURL = baseUrl;
            Filename = "";
        }


        public override UploadResult Upload(Stream stream, string fileName)
        {
            if (string.IsNullOrEmpty(Host))
            {
                throw new Exception("PutWebDav Host is empty.");
            }

            if (string.IsNullOrEmpty(Port))
            {
                Port = "443";
            }

            if (string.IsNullOrEmpty(Directory))
            {
                Directory = "/";
            }

            if (string.IsNullOrEmpty(BaseURL))
            {
                BaseURL = URLHelpers.CombineURL(Host, ":", Port, Directory);
            }

            Filename = fileName;

            // Original, unencoded path. Necessary for shared files
            string path = URLHelpers.CombineURL(Directory, fileName);
            // Encoded path, necessary when sent in the URL
            string encodedPath = URLHelpers.CombineURL(Directory, URLHelpers.URLEncode(fileName));

            string url = URLHelpers.CombineURL(Host, encodedPath);
            url = URLHelpers.FixPrefix(url);

            //NameValueCollection headers = UploadHelpers.CreateAuthenticationHeader(Username, Password);
            //headers["OCS-APIREQUEST"] = "true";

            string response = SendRequest(HttpMethod.PUT, url, stream, UploadHelpers.GetMimeType(fileName), null, null);

            UploadResult result = new UploadResult(response);

            if (!IsError)
            {
                if (CreateShare)
                {
                    AllowReportProgress = false;
                    result.URL = ShareFile(path);
                }
                else
                {
                    result.IsURLExpected = false;
                }
            }

            return result;
        }


        public string ShareFile(string path)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();
            args.Add("path", path); // path to the file/folder which should be shared
            args.Add("shareType", "3"); // ‘0’ = user; ‘1’ = group; ‘3’ = public link
            // args.Add("shareWith", ""); // user / group id with which the file should be shared
            // args.Add("publicUpload", "false"); // allow public upload to a public shared folder (true/false)
            // args.Add("password", ""); // password to protect public link Share with
            args.Add("permissions", "1"); // 1 = read; 2 = update; 4 = create; 8 = delete; 16 = share; 31 = all (default: 31, for public shares: 1)



            string url = URLHelpers.CombineURL(BaseURL, Filename);
            url = URLHelpers.FixPrefix(url);

            NameValueCollection headers = UploadHelpers.CreateAuthenticationHeader(Username, Password);
            headers["OCS-APIREQUEST"] = "true";

            string response = SendRequestMultiPart(url, args, headers);

            if (!string.IsNullOrEmpty(response))
            {
                PutWebDavShareResponse result = JsonConvert.DeserializeObject<PutWebDavShareResponse>(response);

                if (result != null && result.ocs != null && result.ocs.meta != null)
                {
                    if (result.ocs.data != null && result.ocs.meta.statuscode == 100)
                    {
                        PutWebDavShareResponseData data = ((JObject)result.ocs.data).ToObject<PutWebDavShareResponseData>();
                        string link = data.url;

                        return link;
                    }
                    else
                    {
                        Errors.Add(string.Format("Status: {0}\r\nStatus code: {1}\r\nMessage: {2}", result.ocs.meta.status, result.ocs.meta.statuscode, result.ocs.meta.message));
                    }
                }
            }

            return null;
        }

        public class PutWebDavShareResponse
        {
            public PutWebDavShareResponseOcs ocs { get; set; }
        }

        public class PutWebDavShareResponseOcs
        {
            public PutWebDavShareResponseMeta meta { get; set; }
            public object data { get; set; }
        }

        public class PutWebDavShareResponseMeta
        {
            public string status { get; set; }
            public int statuscode { get; set; }
            public string message { get; set; }
        }

        public class PutWebDavShareResponseData
        {
            public int id { get; set; }
            public string url { get; set; }
            public string token { get; set; }
        }
    }
}