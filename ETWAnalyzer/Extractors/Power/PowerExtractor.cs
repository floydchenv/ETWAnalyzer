﻿//// SPDX-FileCopyrightText:  © 2023 Siemens Healthcare GmbH
//// SPDX-License-Identifier:   MIT

using ETWAnalyzer.Extract;
using ETWAnalyzer.Extract.Power;
using ETWAnalyzer.Infrastructure;
using ETWAnalyzer.TraceProcessorHelpers;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ETWAnalyzer.Extractors.Power
{
    /// <summary>
    /// Extract from ETW Provider Microsoft-Windows-Power the currently used power profile
    /// </summary>
    internal class PowerExtractor : ExtractorBase
    {
        /// <summary>
        /// Contains power data when present in trace.
        /// </summary>
        IPendingResult<Microsoft.Windows.EventTracing.Power.IPowerConfigurationDataSource> myPower;

        /// <summary>
        /// Power Event Data which are not parsed by TraceProcssing
        /// </summary>
        IPendingResult<IGenericEventDataSource> myPowerEvents;


        /// <summary>
        /// As long as these settings are missing from TraceProcessing we parse them on our own.
        /// </summary>
        static Dictionary<Guid, Action<IGenericEvent, ETWExtract>> mySettingParsers = new()
        {
            { new Guid("245d8541-3943-4422-b025-13a784f679b7"), ParsePowerProfileBaseGuid },
            { new Guid("8baa4a8a-14c6-4451-8e8b-14bdbd197537"), ParseProcessorAutonomousMode },
            { new Guid("93b8b6dc-0698-4d1c-9ee4-0644e900c85d"), ParseHeteroThreadSchedulingPolicy },
            { new Guid("bae08b81-2d5e-4688-ad6a-13243356654b"), ParseHeteroThreadSchedulingPolicyShort },
            { new Guid("7f2f5cfa-f10c-4823-b5e1-e93ae85f46b5"), ParseHeteroPolicyInEffect },
            { new Guid("31f9f286-5084-42fe-b720-2b0264993763"), ParseActivePowerScheme },
        };

        /// <summary>
        /// Register Power parser.
        /// </summary>
        /// <param name="processor"></param>
        public override void RegisterParsers(ITraceProcessor processor)
        {
            base.RegisterParsers(processor);
            myPower = processor.UsePowerConfigurationData();
            myPowerEvents = processor.UseGenericEvents(KernelPowerConstants.Guid);
        }

        /// <summary>
        /// Extract Power profile data from ETL file.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="results"></param>
        public override void Extract(ITraceProcessor processor, ETWExtract results)
        {
            using var logger = new PerfLogger("Extract Power");

            if (myPower.HasResult)
            {
                PowerConfiguration previous = null;

                foreach (Microsoft.Windows.EventTracing.Power.IPowerConfigurationSnapshot power in myPower.Result.Snapshots)
                {
                    var powerData = new PowerConfiguration();
                    var processorConfig = power.ProcessorConfiguration;
                    var perfConfig = processorConfig.PerformanceConfiguration;
                    var idleConfig = processorConfig.IdleConfiguration;
                    var parkingConfig = processorConfig.ProcessorParkingConfiguration;

                    powerData.TimeSinceTraceStartS = (float)power.Timestamp.RelativeTimestamp.TotalSeconds;
                    powerData.MaxEfficiencyClass1Frequency = (FrequencyValueMHz) processorConfig.MaxEfficiencyClass1Frequency.GetValueOrDefault().TotalMegahertz;
                    powerData.MaxEfficiencyClass0Frequency = (FrequencyValueMHz) processorConfig.MaxEfficiencyClass0Frequency.GetValueOrDefault().TotalMegahertz;

                    powerData.BoostMode = (ProcessorPerformanceBoostMode) perfConfig.BoostMode;
                    powerData.BoostPolicyPercent = (PercentValue) perfConfig.BoostPolicy.Value;
                    powerData.DecreasePolicy = (ProcessorPerformanceChangePolicy) perfConfig.DecreasePolicy;
                    powerData.DecreaseStabilizationInterval = perfConfig.DecreaseStabilizationInterval;
                    powerData.DecreaseThresholdPercent = (PercentValue) perfConfig.DecreaseThreshold.Value;
                    powerData.IncreasePolicy = (ProcessorPerformanceChangePolicy) perfConfig.IncreasePolicy;
                    powerData.IncreaseStabilizationInterval = perfConfig.IncreaseStabilizationInterval;
                    powerData.IncreaseThresholdPercent = (PercentValue) perfConfig.IncreaseThreshold.Value;
                    powerData.LatencySensitivityPerformancePercent = (PercentValue) perfConfig.LatencySensitivityPerformance.Value;
                    powerData.MaxThrottlingFrequencyPercent = (PercentValue) perfConfig.MaxThrottlingFrequency.Value;
                    powerData.MinThrottlingFrequencyPercent = (PercentValue) perfConfig.MinThrottlingFrequency.Value;
                    powerData.StabilizationInterval = perfConfig.StabilizationInterval;
                    powerData.SystemCoolingPolicy = (SystemCoolingPolicy) perfConfig.SystemCoolingPolicy;
                    powerData.ThrottlePolicy = (ProcessorThrottlePolicy) perfConfig.ThrottlePolicy;
                    powerData.TimeWindowSize = perfConfig.TimeWindowSize;

                    powerData.IdleConfiguration.DeepestIdleState = idleConfig.DeepestIdleState;
                    powerData.IdleConfiguration.DemoteThresholdPercent = (PercentValue) idleConfig.DemoteThreshold.Value;
                    powerData.IdleConfiguration.Enabled = idleConfig.Enabled;
                    powerData.IdleConfiguration.MinimumDurationBetweenChecks = TimeSpan.FromTicks(idleConfig.MinimumDurationBetweenChecks.Ticks/1000);  // BUG in TraceProcessing where the duration is parsed as millisecond but the unit is microseconds!
                    powerData.IdleConfiguration.PromoteThresholdPercent = (PercentValue) idleConfig.PromoteThreshold.Value;
                    powerData.IdleConfiguration.ScalingEnabled = idleConfig.ScalingEnabled;

                    powerData.ProcessorParkingConfiguration.ConcurrencyHeadroomThresholdPercent = (PercentValue) parkingConfig.ConcurrencyHeadroomThreshold.Value;
                    powerData.ProcessorParkingConfiguration.ConcurrencyThresholdPercent = (PercentValue) parkingConfig.ConcurrencyThreshold.Value;
                    powerData.ProcessorParkingConfiguration.MaxEfficiencyClass1UnparkedProcessorPercent = (PercentValue) parkingConfig.MaxEfficiencyClass1UnparkedProcessor.Value;
                    powerData.ProcessorParkingConfiguration.MaxUnparkedProcessorPercent = (PercentValue) parkingConfig.MaxUnparkedProcessor.Value;
                    powerData.ProcessorParkingConfiguration.MinEfficiencyClass1UnparkedProcessorPercent = (PercentValue) parkingConfig.MinEfficiencyClass1UnparkedProcessor.Value;
                    powerData.ProcessorParkingConfiguration.MinParkedDuration = parkingConfig.MinParkedDuration;
                    powerData.ProcessorParkingConfiguration.MinUnparkedDuration = parkingConfig.MinUnparkedDuration;
                    powerData.ProcessorParkingConfiguration.MinUnparkedProcessorPercent = (PercentValue) parkingConfig.MinUnparkedProcessor.Value;
                    powerData.ProcessorParkingConfiguration.OverUtilizationThresholdPercent = (PercentValue) parkingConfig.OverUtilizationThreshold.Value;
                    powerData.ProcessorParkingConfiguration.ParkingPerformanceState = (ParkingPerformanceState) parkingConfig.ParkingPerformanceState;
                    powerData.ProcessorParkingConfiguration.ParkingPolicy = (ProcessorParkingPolicy) parkingConfig.ParkingPolicy;
                    powerData.ProcessorParkingConfiguration.UnparkingPolicy = (ProcessorParkingPolicy) parkingConfig.UnparkingPolicy;
                    powerData.ProcessorParkingConfiguration.UtilityDistributionEnabled = parkingConfig.UtilityDistributionEnabled;
                    powerData.ProcessorParkingConfiguration.UtilityDistributionThresholdPercent = (PercentValue) parkingConfig.UtilityDistributionThreshold.Value;

                    // We only save power profile data which is not the same over the duration of the recording.
                    // The default of the wpr Power profile is to record the profile at trace end two times.
                    // Since these are most often identical we spare the space and useless output in -Dump Power command 
                    // by not storing identical data in the resulting Json file.
                    if (!powerData.Equals(previous))
                    {
                        results.PowerConfiguration.Add(powerData);
                        previous = powerData;
                    }
                }

                if (myPowerEvents.HasResult)
                {
                    foreach (var powerEvent in myPowerEvents.Result.Events)
                    {
                        if (powerEvent.Id == KernelPowerConstants.PowerSettingsRundownEventId)
                        {
                            if (mySettingParsers.TryGetValue(powerEvent.Fields[0].AsGuid, out var parser))
                            {
                                parser(powerEvent, results);
                            }
                        }
                    }
                }
            }
        }



        static PowerConfiguration Get(ETWExtract extract)
        {
            return extract.PowerConfiguration.FirstOrDefault();
        }

        private static void ParseHeteroPolicyInEffect(IGenericEvent @event, ETWExtract extract)
        {
            PowerConfiguration cfg = Get(extract);
            if (cfg != null)
            {
                cfg.HeteroPolicyInEffect = (int)ReadUInt32(@event.Fields[2].AsBinary);
            }
        }

        private static void ParseHeteroThreadSchedulingPolicyShort(IGenericEvent @event, ETWExtract extract)
        {
            PowerConfiguration cfg = Get(extract);
            if (cfg != null)
            {
                cfg.HeteroPolicyThreadSchedulingShort = (HeteroThreadSchedulingPolicy)ReadUInt32(@event.Fields[2].AsBinary);
            }

        }

        private static void ParseHeteroThreadSchedulingPolicy(IGenericEvent @event, ETWExtract extract)
        {
            PowerConfiguration cfg = Get(extract);
            if (cfg != null)
            {
                cfg.HeteroPolicyThreadScheduling = (HeteroThreadSchedulingPolicy)ReadUInt32(@event.Fields[2].AsBinary);
            }

        }

        private static void ParseProcessorAutonomousMode(IGenericEvent @event, ETWExtract extract)
        {
            PowerConfiguration cfg = Get(extract);
            if (cfg != null)
            {
                cfg.AutonomousMode = ReadBoolean(@event.Fields[2].AsBinary);
            }

        }

        private static void ParseActivePowerScheme(IGenericEvent @event, ETWExtract extract)
        {
            PowerConfiguration cfg = Get(extract);
            if (cfg != null)
            {
                Guid currentProfile = new Guid(@event.Fields[2].AsBinary.ToArray());
                KernelPowerConstants.BasePowerProfiles.TryGetValue(currentProfile, out var powerProfile);
                cfg.ActivePowerProfile = powerProfile;
                cfg.ActivePowerProfileGuid = currentProfile;
            }
        }

        static void ParsePowerProfileBaseGuid(IGenericEvent ev, ETWExtract extract)
        {
            PowerConfiguration cfg = Get(extract);
            if (cfg != null)
            {
                Guid baseProfile = new Guid(ev.Fields[2].AsBinary.ToArray());
                KernelPowerConstants.BasePowerProfiles.TryGetValue(baseProfile, out var powerProfile);
                cfg.BaseProfile = powerProfile;
            }
        }


        static bool ReadBoolean(IReadOnlyList<byte> data)
        {
            return ReadUInt32(data) != 0;
        }

        static uint ReadUInt32(IReadOnlyList<byte> data)
        {
            return ReadUnmanaged<uint>(data);
        }

        unsafe static T ReadUnmanaged<T>(IReadOnlyList<byte> data) where T : unmanaged
        {
            checked
            {
                ushort num = (ushort)sizeof(T);
                ValidateRemainingLength((ushort)data.Count, num);
                T result = MemoryMarshal.Read<T>(data.ToArray().AsSpan());
                return result;
            }
        }

        static void ValidateRemainingLength(int remainingLength, int neededLength)
        {
            if (remainingLength < neededLength)
            {
                throw new InvalidTraceDataException($"The trace contains an event that failed to parse. {neededLength} bytes were needed but only " + $"{remainingLength} bytes were available.");
            }
        }
    }
}
