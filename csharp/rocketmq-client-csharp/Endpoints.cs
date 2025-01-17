/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Proto = Apache.Rocketmq.V2;

namespace Org.Apache.Rocketmq
{
    public class Endpoints : IEquatable<Endpoints>
    {
        private const string HttpPrefix = "http://";
        private const string HttpsPrefix = "https://";
        private const int DefaultPort = 80;

        private static readonly AddressListEqualityComparer AddressListComparer = new();
        private const string EndpointSeparator = ":";
        private List<Address> Addresses { get; }
        private AddressScheme Scheme { get; }
        private readonly int _hashCode;

        public Endpoints(Proto.Endpoints endpoints)
        {
            Addresses = new List<Address>();
            foreach (var address in endpoints.Addresses)
            {
                Addresses.Add(new Address(address));
            }

            if (!Addresses.Any())
            {
                throw new NotSupportedException("No available address");
            }

            switch (endpoints.Scheme)
            {
                case Proto.AddressScheme.Ipv4:
                    Scheme = AddressScheme.Ipv4;
                    break;
                case Proto.AddressScheme.Ipv6:
                    Scheme = AddressScheme.Ipv6;
                    break;
                case Proto.AddressScheme.DomainName:
                case Proto.AddressScheme.Unspecified:
                default:
                    Scheme = AddressScheme.DomainName;
                    if (Addresses.Count > 1)
                    {
                        throw new NotSupportedException("Multiple addresses are not allowed in domain scheme");
                    }

                    break;
            }

            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + AddressListComparer.GetHashCode(Addresses);
                hash = (hash * 31) + (int)Scheme;
                _hashCode = hash;
            }
        }

        // TODO: Multiple addresses has not been supported yet.
        public Endpoints(string endpoints)
        {
            if (endpoints.StartsWith(HttpPrefix))
            {
                endpoints = endpoints[HttpPrefix.Length..];
            }

            if (endpoints.StartsWith(HttpsPrefix))
            {
                endpoints = endpoints[HttpsPrefix.Length..];
            }

            var index = endpoints.IndexOf(EndpointSeparator, StringComparison.Ordinal);
            var port = index > 0 ? int.Parse(endpoints[(1 + index)..]) : DefaultPort;
            var host = index > 0 ? endpoints.Substring(0, index) : endpoints;

            var uriHostNameType = Uri.CheckHostName(host);
            Scheme = AddressScheme.DomainName;
            switch (uriHostNameType)
            {
                case UriHostNameType.IPv4:
                    Scheme = AddressScheme.Ipv4;
                    break;
                case UriHostNameType.IPv6:
                    Scheme = AddressScheme.Ipv6;
                    break;
                case UriHostNameType.Dns:
                case UriHostNameType.Basic:
                case UriHostNameType.Unknown:
                default:
                    Scheme = AddressScheme.DomainName;
                    break;
            }

            var address = new Address(host, port);
            var addresses = new List<Address> { address };
            Addresses = addresses;
        }

        public override string ToString()
        {
            return GrpcTarget;
        }

        // TODO: Support non-TLS and multiple addresses.
        public string GrpcTarget
        {
            get
            {
                foreach (var address in Addresses)
                {
                    return HttpsPrefix + address.Host + EndpointSeparator + address.Port;
                }

                return "";
            }
        }

        public bool Equals(Endpoints other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Addresses.SequenceEqual(other.Addresses) && Scheme == other.Scheme;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((Endpoints)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public Proto.Endpoints ToProtobuf()
        {
            var endpoints = new Proto.Endpoints();
            foreach (var address in Addresses)
            {
                endpoints.Addresses.Add(address.ToProtobuf());
            }

            endpoints.Scheme = AddressSchemeHelper.ToProtobuf(Scheme);
            return endpoints;
        }
    }
}