﻿namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class MefFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink diagnosticMessageSink;

        /// <summary> 
        /// Initializes a new instance of the <see cref="MefFactDiscoverer"/> class. 
        /// </summary> 
        /// <param name="diagnosticMessageSink">The message sink used to send diagnostic messages</param> 
        public MefFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttributeInfo)
        {
            var methodDisplay = discoveryOptions.MethodDisplayOrDefault();

            yield return new MefFactTestCase(this.diagnosticMessageSink, methodDisplay, testMethod, factAttributeInfo);
        }

        private static IEnumerable<Type> GetNestedTypesRecursively(Type parentType)
        {
            Requires.NotNull(parentType, "parentType");

            foreach (var nested in parentType.GetNestedTypes())
            {
                yield return nested;

                foreach (var recursive in GetNestedTypesRecursively(nested))
                {
                    yield return recursive;
                }
            }
        }

        private class MefFactTestCase : XunitTestCase
        {
            private Type[] parts;
            private IReadOnlyList<string> assemblies;
            private CompositionEngines compositionVersions;
            private bool noCompatGoal;
            private bool invalidConfiguration;

            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete("Called by the de-serializer", true)]
            public MefFactTestCase() { }

            public MefFactTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, IAttributeInfo factAttributeInfo)
                : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
            {
                var factAttribute = MefFactAttribute.Instantiate(factAttributeInfo);
                this.parts = factAttribute.Parts;
                this.assemblies = factAttribute.Assemblies;
                this.compositionVersions = factAttribute.CompositionVersions;
                this.noCompatGoal = factAttribute.NoCompatGoal;
                this.invalidConfiguration = factAttribute.InvalidConfiguration;
            }

            public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                var runSummary = new RunSummary();

                if (parts == null && assemblies == null)
                {
                    parts = GetNestedTypesRecursively(this.TestMethod.TestClass.Class.ToRuntimeType()).Where(t => (!t.IsAbstract || t.IsSealed) && !t.IsInterface).ToArray();
                }

                if (this.compositionVersions.HasFlag(CompositionEngines.V1))
                {
                    var runner = new MefTestCommand(this, "V1", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, CompositionEngines.V1, parts, this.assemblies, this.invalidConfiguration);
                    runSummary.Aggregate(await runner.RunAsync());
                }

                if (this.compositionVersions.HasFlag(CompositionEngines.V2))
                {
                    var runner = new MefTestCommand(this, "V2", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, CompositionEngines.V2, parts, this.assemblies, this.invalidConfiguration);
                    runSummary.Aggregate(await runner.RunAsync());
                }

                if ((this.compositionVersions & CompositionEngines.V3EnginesMask) == CompositionEngines.Unspecified)
                {
                    if (!this.noCompatGoal)
                    {
                        // Call out that we're *not* testing V3 functionality for this test.
                        if (!messageBus.QueueMessage(new TestSkipped(new XunitTest(this, "V3"), "Test does not include V3 test.")))
                        {
                            cancellationTokenSource.Cancel();
                        }
                    }
                }
                else
                {
                    var v3DiscoveryTest = new MefV3DiscoveryTestCommand(this, "V3 composition", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, this.compositionVersions, parts ?? new Type[0], this.assemblies ?? ImmutableList<string>.Empty, this.invalidConfiguration);
                    runSummary.Aggregate(await v3DiscoveryTest.RunAsync());

                    if (v3DiscoveryTest.Passed && (!this.invalidConfiguration || this.compositionVersions.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors)))
                    {
                        foreach (var configuration in v3DiscoveryTest.ResultingConfigurations)
                        {
                            if (!this.compositionVersions.HasFlag(CompositionEngines.V3SkipCodeGenScenario))
                            {
                                // TODO: Uncomment these lines after getting codegen to work again.
                                //       Also re-enable some codegen tests by removing 'abstract' from classes that have this comment:
                                //       // TODO: remove "abstract" from the class definition to re-enable these tests when codegen is fixed.
                                ////var codeGenRunner = new Mef3TestCommand(this, "V3 engine (codegen)", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, configuration, this.compositionVersions, runtime: false);
                                ////runSummary.Aggregate(await codeGenRunner.RunAsync());
                            }

                            var runner = new Mef3TestCommand(this, "V3 engine (runtime)", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, configuration, this.compositionVersions, runtime: true);
                            runSummary.Aggregate(await runner.RunAsync());
                        }
                    }
                }

                return runSummary;
            }

            public override void Serialize(IXunitSerializationInfo data)
            {
                base.Serialize(data);
                data.AddValue(nameof(parts), this.parts);
                data.AddValue(nameof(assemblies), this.assemblies);
                data.AddValue(nameof(compositionVersions), this.compositionVersions);
                data.AddValue(nameof(noCompatGoal), this.noCompatGoal);
                data.AddValue(nameof(invalidConfiguration), this.invalidConfiguration);
            }

            public override void Deserialize(IXunitSerializationInfo data)
            {
                base.Deserialize(data);
                this.parts = data.GetValue<Type[]>(nameof(parts));
                this.assemblies = data.GetValue<IReadOnlyList<string>>(nameof(assemblies));
                this.compositionVersions = data.GetValue<CompositionEngines>(nameof(compositionVersions));
                this.noCompatGoal = data.GetValue<bool>(nameof(noCompatGoal));
                this.invalidConfiguration = data.GetValue<bool>(nameof(invalidConfiguration));
            }
        }
    }
}
