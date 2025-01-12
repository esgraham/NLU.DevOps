﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace NLU.DevOps.CommandLine.Tests.Train
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using CommandLine.Train;
    using FluentAssertions;
    using global::CommandLine;
    using NUnit.Framework;

    [TestFixture]
    internal sealed class TrainCommandTests : IDisposable
    {
        private ICommand commandUnderTest;
        private List<string> options;

        [SetUp]
        public void SetUp()
        {
            this.options = new List<string>();
            this.options.Add("-s");
            this.options.Add("luis");
        }

        [TearDown]
        public void TearDown()
        {
            this.commandUnderTest?.Dispose();
        }

        [Test]
        public void WhenArgumentsDoNotIncludeUtterancesOrSettings()
        {
            this.WhenParserIsRun();
            Action a = () => this.commandUnderTest.Main();
            a.Should().Throw<InvalidOperationException>().WithMessage("Must specify either --utterances or --service-settings when using train.");
        }

        [Test]
        public void WhenArgumentsIncludeUtterancesValueButFileDoesNotExist()
        {
            this.options.Add("-u");
            this.options.Add("./bogusfolder/utterances.json");
            this.WhenParserIsRun();
            Action a = () => this.commandUnderTest.Main();
            a.Should().Throw<DirectoryNotFoundException>();
        }

        [Test]
        public void SaveAppsettingsShouldCreateAFile()
        {
            this.options.Add("-u");
            this.options.Add("./testdata/utterances.json");
            this.options.Add("-a");
            this.WhenParserIsRun();
            this.commandUnderTest.Main();
            File.Exists("appsettings.luis.json").Should().BeTrue();
            File.Delete("appsettings.luis.json");
        }

        public void Dispose()
        {
            this.commandUnderTest?.Dispose();
        }

        private void WhenParserIsRun()
        {
            var args = this.options.ToArray();
            var results = Parser.Default.ParseArguments<TrainOptions>(args);
            var trainOptions = (Parsed<TrainOptions>)results;
            this.commandUnderTest = new TrainCommandMock(trainOptions.Value);
        }
    }
}
