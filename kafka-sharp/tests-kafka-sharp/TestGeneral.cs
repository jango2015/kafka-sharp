﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafka.Cluster;
using Kafka.Protocol;
using Kafka.Public;
using NUnit.Framework;
using Cluster = Kafka.Public.Cluster;

namespace tests_kafka_sharp
{
    [TestFixture]
    internal class TestGeneral
    {
        private Cluster InitCluster(Configuration configuration, ILogger logger, MetadataResponse metadata, bool forceErrors = false, bool forceConnectionErrors = false)
        {
            TestData.Reset();
            var cluster = new Kafka.Cluster.Cluster(
                configuration,
                logger,
                (h, p) =>
                    new Node(string.Format("[{0}:{1}]", h, p), () => new EchoConnectionMock(forceConnectionErrors),
                        new ScenarioSerializerMock(metadata, forceErrors), configuration).SetResolution(1),
                null);
            return new Cluster(configuration, logger, cluster);
        }

        [Test]
        public void TestOneProduce()
        {
            var logger = new TestLogger();
            var configuration = new Configuration
            {
                BatchSize = 10,
                BufferingTime = TimeSpan.FromMilliseconds(15),
                ErrorStrategy = ErrorStrategy.Discard,
                Seeds = "localhost:1,localhost:2,localhost:3"
            };
            var cluster = InitCluster(configuration, logger, TestData.TestMetadataResponse);

            cluster.Produce("topic1", "key", "value");
            SpinWait.SpinUntil(() => cluster.Statistics.Exit == 1);
            cluster.Dispose();
            var statistics = cluster.Statistics;
            Assert.AreEqual(1, statistics.Exit);
            Assert.AreEqual(1, statistics.SuccessfulSent);
            Assert.AreEqual(0, statistics.Errors);
            Assert.AreEqual(0, statistics.Expired);
            Assert.AreEqual(0, statistics.Discarded);
            Assert.AreEqual(0, statistics.NodeDead);
            Assert.GreaterOrEqual(statistics.ResponseReceived, 2); // 1 produce, 1 or more fetch metadata
            Assert.GreaterOrEqual(statistics.RequestSent, 2); // 1 produce response, 1 or more fetch metadata response
            Assert.GreaterOrEqual(logger.InformationLog.Count(), 3); // Fetch metadata feedback
            Assert.AreEqual(0, logger.ErrorLog.Count());
            Assert.AreEqual(0, logger.WarningLog.Count());
        }

        [Test]
        public void TestMultipleProduce()
        {
            var logger = new TestLogger();
            var configuration = new Configuration
            {
                BatchSize = 10,
                BufferingTime = TimeSpan.FromMilliseconds(15),
                ErrorStrategy = ErrorStrategy.Discard,
                Seeds = "localhost:1,localhost:2,localhost:3"
            };
            var cluster = InitCluster(configuration, logger, TestData.TestMetadataResponse);

            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");

            SpinWait.SpinUntil(() => cluster.Statistics.Exit == 14);
            cluster.Dispose();
            var statistics = cluster.Statistics;
            Assert.AreEqual(14, statistics.Exit);
            Assert.AreEqual(14, statistics.SuccessfulSent);
            Assert.AreEqual(0, statistics.Errors);
            Assert.AreEqual(0, statistics.Expired);
            Assert.AreEqual(0, statistics.Discarded);
            Assert.AreEqual(0, statistics.NodeDead);
            Assert.GreaterOrEqual(statistics.ResponseReceived, 3); // 2 or more produce, 1 or more fetch metadata
            Assert.GreaterOrEqual(statistics.RequestSent, 3); // 2 or more produce response, 1 or more fetch metadata response
            Assert.GreaterOrEqual(logger.InformationLog.Count(), 3); // Fetch metadata feedback
            Assert.AreEqual(0, logger.ErrorLog.Count());
            Assert.AreEqual(0, logger.WarningLog.Count());
        }

        [Test]
        public void TestMultipleProduceWithErrorsAndDiscard()
        {
            var logger = new TestLogger();
            var configuration = new Configuration
            {
                BatchSize = 10,
                BufferingTime = TimeSpan.FromMilliseconds(15),
                ErrorStrategy = ErrorStrategy.Discard,
                Seeds = "localhost:1,localhost:2,localhost:3"
            };
            var cluster = InitCluster(configuration, logger, TestData.TestMetadataResponse, true);

            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");

            SpinWait.SpinUntil(() => cluster.Statistics.Exit == 14);
            cluster.Dispose();
            var statistics = cluster.Statistics;
            Assert.AreEqual(14, statistics.Exit);
            Assert.GreaterOrEqual(statistics.SuccessfulSent, 1);
            Assert.GreaterOrEqual(statistics.Errors, 0);
            Assert.AreEqual(0, statistics.Expired);
            Assert.GreaterOrEqual(statistics.Discarded, 1); // At least once an irrecoverable error
            Assert.AreEqual(0, statistics.NodeDead);
            Assert.GreaterOrEqual(statistics.ResponseReceived, 2); // 1 or more successful produce, 1 or more fetch metadata
            Assert.GreaterOrEqual(statistics.RequestSent, 3); // 2 or more produce response, 1 or more fetch metadata response
            Assert.GreaterOrEqual(logger.InformationLog.Count(), 3); // Fetch metadata feedback
            Assert.AreEqual(0, logger.ErrorLog.Count());
            Assert.AreEqual(0, logger.WarningLog.Count());
        }

        [Test]
        public void TestMultipleProduceWithNetworkErrorsAndRetry()
        {
            var logger = new TestLogger();
            var configuration = new Configuration
            {
                BatchSize = 10,
                BufferingTime = TimeSpan.FromMilliseconds(15),
                ErrorStrategy = ErrorStrategy.Retry,
                Seeds = "localhost:1,localhost:2,localhost:3"
            };
            var cluster = InitCluster(configuration, logger, TestData.TestMetadataResponse, false, true);

            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");

            SpinWait.SpinUntil(() => cluster.Statistics.Exit == 14);
            cluster.Dispose();
            var statistics = cluster.Statistics;
            Assert.AreEqual(14, statistics.Exit);
            Assert.GreaterOrEqual(statistics.SuccessfulSent, 1);
            Assert.GreaterOrEqual(statistics.Errors, 1);
            Assert.AreEqual(0, statistics.Expired);
            Assert.AreEqual(0, statistics.Discarded); // only network errors and we retry
            Assert.AreEqual(0, statistics.NodeDead);
            Assert.GreaterOrEqual(statistics.ResponseReceived, 3); // 2 or more successful produce, 1 or more fetch metadata
            Assert.GreaterOrEqual(statistics.RequestSent, 3); // 2 or more produce response, 1 or more fetch metadata response
            Assert.GreaterOrEqual(logger.InformationLog.Count(), 3); // Fetch metadata feedback
            Assert.GreaterOrEqual(logger.ErrorLog.Count(), 1);
            Assert.AreEqual(0, logger.WarningLog.Count());
        }

        [Test]
        public void TestBigShake()
        {
            var logger = new TestLogger();
            var configuration = new Configuration
            {
                BatchSize = 10,
                BufferingTime = TimeSpan.FromMilliseconds(15),
                ErrorStrategy = ErrorStrategy.Retry,
                Seeds = "localhost:1,localhost:2,localhost:3"
            };
            var cluster = InitCluster(configuration, logger, TestData.TestMetadataResponse, true, true);

            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic2", "key", "value");
            cluster.Produce("topic1", "key", "value");
            cluster.Produce("topic3", "key", "value");
            cluster.Produce("topic1", "key", "value");

            SpinWait.SpinUntil(() => cluster.Statistics.Exit == 14);
            cluster.Dispose();
            var statistics = cluster.Statistics;
            Assert.AreEqual(14, statistics.Exit);
            Assert.GreaterOrEqual(statistics.SuccessfulSent, 1);
            Assert.GreaterOrEqual(statistics.Errors, 1);
            Assert.AreEqual(0, statistics.Expired);
            Assert.AreEqual(0, statistics.NodeDead);
            Assert.GreaterOrEqual(statistics.ResponseReceived, 3); // 2 or more successful produce, 1 or more fetch metadata
            Assert.GreaterOrEqual(statistics.RequestSent, 3); // 2 or more produce response, 1 or more fetch metadata response
            Assert.GreaterOrEqual(logger.InformationLog.Count(), 3); // Fetch metadata feedback
            Assert.GreaterOrEqual(logger.ErrorLog.Count(), 1);
            Assert.AreEqual(0, logger.WarningLog.Count());
        }
    }
}