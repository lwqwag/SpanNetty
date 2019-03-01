﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Cookies
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    using static CookieUtil;

    public sealed class ClientCookieEncoder : CookieEncoder
    {
        // Strict encoder that validates that name and value chars are in the valid scope and (for methods that accept
        // multiple cookies) sorts cookies into order of decreasing path length, as specified in RFC6265.
        public static readonly ClientCookieEncoder StrictEncoder = new ClientCookieEncoder(true);

        // Lax instance that doesn't validate name and value, and (for methods that accept multiple cookies) keeps
        // cookies in the order in which they were given.
        public static readonly ClientCookieEncoder LaxEncoder = new ClientCookieEncoder(false);

        static readonly CookieComparer Comparer = new CookieComparer();

        ClientCookieEncoder(bool strict) : base(strict)
        {
        }

        public string Encode(string name, string value) => this.Encode(new DefaultCookie(name, value));

        public string Encode(ICookie cookie)
        {
            if (null == cookie) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cookie); }

            StringBuilder buf = StringBuilder();
            this.Encode(buf, cookie);
            return StripTrailingSeparator(buf);
        }

        sealed class CookieComparer : IComparer<ICookie>
        {
            public int Compare(ICookie c1, ICookie c2)
            {
                Debug.Assert(c1 != null && c2 != null);

                string path1 = c1.Path;
                string path2 = c2.Path;
                // Cookies with unspecified path default to the path of the request. We don't
                // know the request path here, but we assume that the length of an unspecified
                // path is longer than any specified path (i.e. pathless cookies come first),
                // because setting cookies with a path longer than the request path is of
                // limited use.
                int len1 = path1?.Length ?? int.MaxValue;
                int len2 = path2?.Length ?? int.MaxValue;
                int diff = len2 - len1;
                if (diff != 0)
                {
                    return diff;
                }
                // Rely on Java's sort stability to retain creation order in cases where
                // cookies have same path length.
                return -1;
            }
        }

        public string Encode(params ICookie[] cookies)
        {
            if (cookies == null || 0u >= (uint)cookies.Length)
            {
                return null;
            }

            StringBuilder buf = StringBuilder();
            if (this.Strict)
            {
                if (cookies.Length == 1)
                {
                    this.Encode(buf, cookies[0]);
                }
                else
                {
                    var cookiesSorted = new ICookie[cookies.Length];
                    Array.Copy(cookies, cookiesSorted, cookies.Length);
                    Array.Sort(cookiesSorted, Comparer);
                    foreach(ICookie c in cookiesSorted)
                    {
                        this.Encode(buf, c);
                    }
                }
            }
            else
            {
                foreach (ICookie c in cookies)
                {
                    this.Encode(buf, c);
                }
            }
            return StripTrailingSeparatorOrNull(buf);
        }

        public string Encode(IEnumerable<ICookie> cookies)
        {
            if (null == cookies) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cookies); }

            StringBuilder buf = StringBuilder();
            if (this.Strict)
            {
                var cookiesList = cookies.ToList();
                cookiesList.Sort(Comparer);
                foreach (ICookie c in cookiesList)
                {
                    this.Encode(buf, c);
                }
            }
            else
            {
                foreach (ICookie cookie in cookies)
                {
                    this.Encode(buf, cookie);
                }
            }
            return StripTrailingSeparatorOrNull(buf);
        }

        void Encode(StringBuilder buf, ICookie c)
        {
            string name = c.Name;
            string value = c.Value?? "";

            this.ValidateCookie(name, value);

            if (c.Wrap)
            {
                AddQuoted(buf, name, value);
            }
            else
            {
                Add(buf, name, value);
            }
        }
    }
}
