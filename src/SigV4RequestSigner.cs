using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Amazon.Runtime;
using Amazon;

namespace Amazon.Neptune.Gremlin.Driver
{
    public class SigV4RequestSigner
    {
        private readonly string _access_key;
        private readonly string _secret_key;
        private readonly string _token;
        private readonly string _region;
        private readonly SHA256 _sha256;
        private const string algorithm = "AWS4-HMAC-SHA256";
        private const string DefaultRegion = "us-east-1";

        /* Constructor
         *
         *
         *
         *
         */
        public SigV4RequestSigner()
        {
            ImmutableCredentials awsCredentials = FallbackCredentialsFactory.GetCredentials().GetCredentials();
            RegionEndpoint region = FallbackRegionFactory.GetRegionEndpoint();
            
            _access_key = awsCredentials.AccessKey;
            _secret_key = awsCredentials.SecretKey;
            _token = awsCredentials.Token;
            _region = region?.SystemName ?? DefaultRegion; //ex: set via AWS_REGION env variable 
            _sha256 = SHA256.Create();
        }


        /******************** AWS SIGNING FUNCTIONS *********************/
        private string Hash(byte[] bytesToHash)
        {
            var result = _sha256.ComputeHash(bytesToHash);
            return ToHexString(result);
        }

        private static byte[] HmacSHA256(byte[] key, string data)
        {
            var hashAlgorithm = new HMACSHA256(key);
            return hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            byte[] kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
            byte[] kDate = HmacSHA256(kSecret, dateStamp);
            byte[] kRegion = HmacSHA256(kDate, regionName);
            byte[] kService = HmacSHA256(kRegion, serviceName);
            byte[] kSigning = HmacSHA256(kService, "aws4_request");
            return kSigning;
        }

        private static string ToHexString(byte[] array)
        {
            var hex = new StringBuilder(array.Length * 2);
            foreach (byte b in array) {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
        
        public Action<ClientWebSocketOptions> signRequest(string hostname, int port)
        {
            var neptune_endpoint = hostname + ":" + port;
            var request = new HttpRequestMessage {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://" + neptune_endpoint + "/gremlin")
            };
            var signedrequest = this.Sign(request, "neptune-db", _region, _token);

            return new Action<ClientWebSocketOptions>(options => { 
                    options.SetRequestHeader("host", neptune_endpoint);
                    options.SetRequestHeader("x-amz-date", signedrequest.Headers.GetValues("x-amz-date").FirstOrDefault());
                    if (signedrequest.Headers.TryGetValues("x-amz-security-token", out var values)) {
                        options.SetRequestHeader("x-amz-security-token", values.FirstOrDefault());
                    }
                    options.SetRequestHeader("Authorization", signedrequest.Headers.GetValues("Authorization").FirstOrDefault());
                    }); 
        }

        public HttpRequestMessage Sign(HttpRequestMessage request, string service, string region, string sessionToken = null)
        {
            var amzdate = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var datestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
            var canonical_request = new StringBuilder();
            var canonicalQueryParams = "";
            var signedHeadersList = new List<string>();
            var signed_headers = "";
            var content = new byte[0];
            var payload_hash = Hash(content);
            var credential_scope = $"{datestamp}/{region}/{service}/aws4_request";
            
            if (string.IsNullOrEmpty(service)) {
                throw new ArgumentOutOfRangeException(nameof(service), service, "Not a valid service.");
            }

            if (string.IsNullOrEmpty(region)) {
                throw new ArgumentOutOfRangeException(nameof(region), region, "Not a valid region.");
            }

            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Headers.Host == null) {
                request.Headers.Host = request.RequestUri.Host + ":" + request.RequestUri.Port;
            }
            
            if (!string.IsNullOrEmpty(sessionToken)) {
                request.Headers.Add("x-amz-security-token",sessionToken);
            }

            request.Headers.Add("x-amz-date", amzdate);

            canonicalQueryParams = GetCanonicalQueryParams(request);

            canonical_request.Append(request.Method + "\n");
            canonical_request.Append(request.RequestUri.AbsolutePath + "\n");
            canonical_request.Append(canonicalQueryParams + "\n");

            foreach (var header in request.Headers.OrderBy(a => a.Key.ToLowerInvariant()))
            {
                canonical_request.Append(header.Key.ToLowerInvariant());
                canonical_request.Append(":");
                canonical_request.Append(string.Join(",", header.Value.Select(s => s.Trim())));
                canonical_request.Append("\n");
                signedHeadersList.Add(header.Key.ToLowerInvariant());
            }
            canonical_request.Append("\n");
            
            signed_headers = string.Join(";", signedHeadersList);
            canonical_request.Append(signed_headers + "\n");
            canonical_request.Append(payload_hash);
            
            var string_to_sign = $"{algorithm}\n{amzdate}\n{credential_scope}\n" + Hash(Encoding.UTF8.GetBytes(canonical_request.ToString()));

            var signing_key = GetSignatureKey(_secret_key, datestamp, region, service);
            var signature = ToHexString(HmacSHA256(signing_key, string_to_sign));
            
            request.Headers.TryAddWithoutValidation("Authorization", $"{algorithm} Credential={_access_key}/{credential_scope}, SignedHeaders={signed_headers}, Signature={signature}");
            
            return request;
        }

        private static string GetCanonicalQueryParams(HttpRequestMessage request)
        {
            var querystring = HttpUtility.ParseQueryString(request.RequestUri.Query);
            var keys = querystring.AllKeys.OrderBy(a => a).ToArray();

            // Query params must be escaped in upper case (i.e. "%2C", not "%2c").
            var queryParams = keys.Select(key => $"{key}={Uri.EscapeDataString(querystring[key])}");
            var canonicalQueryParams = string.Join("&", queryParams);
            return canonicalQueryParams;
        }
    }
}
