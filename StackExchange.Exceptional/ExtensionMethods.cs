﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;

namespace StackExchange.Exceptional.Extensions
{
    /// <summary>
    /// Extenstion methods used inside of StackExchange.Exceptional
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Returns a humanized string indicating how long ago something happened, eg "3 days ago".
        /// For future dates, returns when this DateTime will occur from DateTime.UtcNow.
        /// </summary>
        public static string ToRelativeTime(this DateTime dt)
        {
            DateTime utcNow = DateTime.UtcNow;
            return dt <= utcNow ? ToRelativeTimePast(dt, utcNow) : ToRelativeTimeFuture(dt, utcNow);
        }

        private static string ToRelativeTimePast(DateTime dt, DateTime utcNow)
        {
            TimeSpan ts = utcNow - dt;
            double delta = ts.TotalSeconds;

            if (delta < 60)
            {
                return ts.Seconds == 1 ? "1 sec ago" : ts.Seconds + " secs ago";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes == 1 ? "1 min ago" : ts.Minutes + " mins ago";
            }
            if (delta < 86400)  // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours == 1 ? "1 hour ago" : ts.Hours + " hours ago";
            }

            int days = ts.Days;
            if (days == 1)
            {
                return "yesterday";
            }
            if (days <= 2)
            {
                return days + " days ago";
            }
            if (days <= 330)
            {
                return dt.ToString("MMM %d 'at' %H:mmm");
            }
            return dt.ToString(@"MMM %d \'yy 'at' %H:mmm");
        }

        private static string ToRelativeTimeFuture(DateTime dt, DateTime utcNow)
        {
            TimeSpan ts = dt - utcNow;
            double delta = ts.TotalSeconds;

            if (delta < 60)
            {
                return ts.Seconds == 1 ? "in 1 second" : "in " + ts.Seconds + " seconds";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes == 1 ? "in 1 minute" : "in " + ts.Minutes + " minutes";
            }
            if (delta < 86400) // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours == 1 ? "in 1 hour" : "in " + ts.Hours + " hours";
            }

            // use our own rounding so we can round the correct direction for future
            var days = (int)Math.Round(ts.TotalDays, 0);
            if (days == 1)
            {
                return "tomorrow";
            }
            if (days <= 10)
            {
                return "in " + days + " day" + (days > 1 ? "s" : "");
            }
            if (days <= 330)
            {
                return "on " + dt.ToString("MMM %d 'at' %H:mmm");
            }
            return "on " + dt.ToString(@"MMM %d \'yy 'at' %H:mmm");
        }

        /// <summary>
        /// force string to be maxlen or smaller
        /// </summary>
        public static string Truncate(this string s, int maxLength)
        {
            return (s.HasValue() && s.Length > maxLength) ? s.Remove(maxLength) : s;
        }

        /// <summary>
        /// If this String is over 'maxLength', answers a new String with Length = 'maxLength', with ...
        /// as the final three characters.
        /// </summary>
        public static string TruncateWithEllipsis(this string s, int maxLength)
        {
            const string ellipsis = "...";
            return (s.HasValue() && s.Length > maxLength) ? (s.Truncate(maxLength - ellipsis.Length) + ellipsis) : s;
        }

        /// <summary>
        /// Answers true if this String is either null or empty.
        /// </summary>
        /// <remarks>I'm so tired of typing String.IsNullOrEmpty(s)</remarks>
        public static bool IsNullOrEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// Returns the first non-null/non-empty parameter when this String is null/empty.
        /// </summary>
        public static string IsNullOrEmptyReturn(this string s, params string[] otherPossibleResults)
        {
            if (s.HasValue())
                return s;
            
            foreach (string t in otherPossibleResults ?? new string[0])
            {
                if (t.HasValue())
                    return t;
            }

            return "";
        }

        /// <summary>
        /// Answers true if this String is neither null or empty.
        /// </summary>
        /// <remarks>I'm also tired of typing !String.IsNullOrEmpty(s)</remarks>
        public static bool HasValue(this string s)
        {
            return !IsNullOrEmpty(s);
        }
        
        /// <summary>
        /// When a client IP can't be determined
        /// </summary>
        public const string UnknownIP = "0.0.0.0";
        
        /// <summary>
        /// returns true if this is a private network IP  
        /// http://en.wikipedia.org/wiki/Private_network
        /// </summary>
        private static bool IsPrivateIP(string ipString)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(ipString, out ipAddress)) return false;
            
            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = ipAddress.GetAddressBytes();
                switch (bytes[0])
                {
                    case 10:
                        return true;
                    case 172:
                        return bytes[1] < 32 && bytes[1] >= 16;
                    case 192:
                        return bytes[1] == 168;
                    case 127:
                        return bytes[1] == 0 && bytes[2] == 0;
                    default:
                        return false;
                }
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // equivalent of 127.0.0.1 in IPv6
                if (ipString == "::1")
                    return true;
                // The original IPv6 Site Local addresses (fec0::/10) are deprecated. Unfortunately IsIPv6SiteLocal only checks for the original deprecated version:
                if (ipAddress.IsIPv6SiteLocal)
                    return true;
                // These days Unique Local Addresses (ULA) are used in place of Site Local. 
                // ULA has two variants: 
                //      fc00::/8 is not defined yet, but might be used in the future for internal-use addresses that are registered in a central place (ULA Central). 
                //      fd00::/8 is in use and does not have to registered anywhere.
                var firstWord = ipString.Split(new[] {':'}, StringSplitOptions.RemoveEmptyEntries)[0];
                if (firstWord.Length >= 4 && firstWord.Substring(0, 2) == "fc")
                    return true;
                if (firstWord.Length >= 4 && firstWord.Substring(0, 2) == "fd")
                    return true;
                // Link local addresses (prefixed with fe80) are not routable
                if (firstWord == "fe80")
                    return true;
                // Discard Prefix
                if (firstWord == "100")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// retrieves the IP address of the current request -- handles proxies and private networks
        /// </summary>
        public static string GetRemoteIP(this NameValueCollection serverVariables)
        {
            var ip = serverVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            var ipForwarded = serverVariables["HTTP_X_FORWARDED_FOR"];
            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                ip = ipForwarded;

            return ip.HasValue() ? ip : UnknownIP;
        }

        /// <summary>
        /// Converts a string to a guid, or empty guid if empty or invalid
        /// </summary>
        public static Guid ToGuid(this string input)
        {
            Guid result;
            Guid.TryParse(input, out result);
            return result;
        }

        /// <summary>
        /// Strips non-essential characters from a guid
        /// </summary>
        public static string ToFileName(this Guid guid)
        {
            return guid.ToString().Replace("-", "");
        }
        
        /// <summary>
        /// Gets the short exception name, e.g. System.IndexOutOfRange returns just IndexOutOfRange
        /// </summary>
        public static string ToShortException(this string type)
        {
            if (type.IsNullOrEmpty()) return "";
            var shortType = type.Split('.').Last();

            const string suffix = "Exception";

            if (shortType.EndsWith(suffix) && shortType != suffix)
                return shortType.Substring(0, shortType.Length - suffix.Length);
            return shortType;
        }

        /// <summary>
        /// Resolves ~\ relative paths if needed
        /// </summary>
        public static string ResolvePath(this string path)
        {
            if (path.IsNullOrEmpty()) return "";
            if (path.StartsWith(@"~\"))
                return AppDomain.CurrentDomain.GetData("APPBASE") + path.Substring(2);
            return path;
        }

        /// <summary>
        /// Resolves ~/ relative paths if needed
        /// </summary>
        public static string ResolveRelativeUrl(this string relativeUrl)
        {
            if (relativeUrl.IsNullOrEmpty()) return "";
            if (relativeUrl.StartsWith(@"~/"))
                return VirtualPathUtility.ToAbsolute(relativeUrl);
            return relativeUrl;
        }


        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        /// <summary>
        /// Returns a unix Epoch time given a Date
        /// </summary>
        public static long ToEpochTime(this DateTime dt)
        {
            return (long)(dt - Epoch).TotalSeconds;
        }

        /// <summary>
        /// Returns a unix Epoch time if given a value, and null otherwise.
        /// </summary>
        public static long? ToEpochTime(this DateTime? dt)
        {
            return dt.HasValue ? (long?)ToEpochTime(dt.Value) : null;
        }

        /// <summary>
        /// Takes a NameValuePair collection and reduces it down to a JSON object in kay/value pair form
        /// </summary>
        /// <remarks>
        /// This is not technically correct for all cases, since a querystring can contain multiple 
        /// occurences of the same variable for example, this reduces it down to 1 occurence for the acessibility tradeoff
        /// </remarks>
        public static Dictionary<string, string> ToJsonDictionary(this List<Error.NameValuePair> collection)
        {
            var result = new Dictionary<string, string>();
            if (collection == null) return result;
            foreach(var pair in collection)
            {
                if (pair.Name.HasValue())
                {
                    result[pair.Name] = pair.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Turns a dictionary into a name value collection, for code reuse
        /// </summary>
        public static NameValueCollection ToNameValueCollection(this Dictionary<string, string> dict)
        {
            if (dict == null) return new NameValueCollection();

            var result = new NameValueCollection(dict.Count);
            foreach (var kvp in dict)
            {
                result.Add(kvp.Key, kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Returns a JSON string representing this object
        /// </summary>
        public static string ToJson(this object o)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(o);
        }
    }
}