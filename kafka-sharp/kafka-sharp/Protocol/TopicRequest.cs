﻿// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using Kafka.Common;

namespace Kafka.Protocol
{
    struct TopicRequest : ISerializableRequest
    {
        public string[] Topics;

        #region Serialization

        public ReusableMemoryStream Serialize(ReusableMemoryStream target, int correlationId, byte[] clientId, object noextra)
        {
            return CommonRequest.Serialize(target, this, correlationId, clientId, Basics.ApiKey.MetadataRequest, null);
        }

        public void SerializeBody(ReusableMemoryStream stream, object noextra)
        {
            if (Topics == null || Topics.Length == 0)
            {
                stream.Write(Basics.Zero32, 0, 4);
            }
            else
            {
                BigEndianConverter.Write(stream, Topics.Length);
                foreach (var t in Topics)
                    Basics.SerializeString(stream, t);
            }
        }

        #endregion
    }
}