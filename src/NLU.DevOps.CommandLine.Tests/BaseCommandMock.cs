﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace NLU.DevOps.CommandLine.Tests
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;
    using Models;
    using Moq;

    internal class BaseCommandMock : BaseCommand<BaseOptions>
    {
        public BaseCommandMock(BaseOptions options)
            : base(options)
        {
        }

        public new ILogger Logger => base.Logger;

        public static new void Write(string path, object value)
        {
            BaseCommand<BaseOptions>.Write(path, value);
        }

        public override int Main()
        {
            throw new NotImplementedException();
        }

        protected override INLUTrainClient CreateNLUTrainClient()
        {
            return new Mock<INLUTrainClient>().Object;
        }

        protected override INLUTestClient CreateNLUTestClient()
        {
            return new Mock<INLUTestClient>().Object;
        }
    }
}
