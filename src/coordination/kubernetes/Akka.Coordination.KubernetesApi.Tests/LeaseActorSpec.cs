//-----------------------------------------------------------------------
// <copyright file="LeaseActorSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit;
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Tests
{

    public class LeaseActorSpec : LeaseActorTest
    {
        public LeaseActorSpec(ITestOutputHelper output) : base(nameof(LeaseActorSpec), output)
        {
        }
        
        // TODO: what if the same client asks for the lease when granting? respond to both or ignore?
        
        [Fact(DisplayName = "LeaseActor should acquire empty lease")]
        public void AcquireEmptyLease()
        {
            RunTest(() =>
            {
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));

                // as no one owns the lock get the lock
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
            });
        }

        [Fact(DisplayName = "LeaseActor should handle failure in initial read")]
        public void HandleFailureInInitialRead()
        {
            RunTest(() =>
            {
                var failure = new LeaseException("Failed to communicate with API server");
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new Status.Failure(failure));
                SenderProbe.ExpectMsg<Status.Failure>().Cause.Should().Be(failure);
            });
        }

        [Fact(DisplayName = "LeaseActor should allow acquire after initial failure on rad")]
        public void AcquireAfterInitialFailure()
        {
            RunTest(() =>
            {
                K8SApiFailureDuringRead();
                AcquireLease();
            });
        }

        [Fact(DisplayName = "LeaseActor should allow client to re-acquire the same lease")]
        public void ClientReAcquireTheSameLease()
        {
            RunTest(() =>
            {
                AcquireLease();
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
            });
        }

        [Fact(DisplayName = "LeaseActor should fail if granting takes longer than the heart beat timeout")]
        public async Task FailIfGrantingIsLongerThanHeartBeatTimeout()
        {
            await RunTestAsync(async () =>
            {
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            
                // too slow, could have already timed out
                await Task.Delay(LeaseSettings.TimeoutSettings.HeartbeatTimeout * 2);
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
            
                // not granted
                SenderProbe.ExpectMsg<Status.Failure>().Cause.Message
                    .Should().StartWith("API server took too long to respond");
                Granted.Value.Should().BeFalse();
            
                // should allow retry
                AcquireLease();
            });
        }
        
        // FIXME, give up if API server is constantly slow to respond

        [Fact(DisplayName = "LeaseActor should reject taken lease in idle state")]
        public void RejectTakenLeaseInIdleState()
        {
            RunTest(() =>
            {
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new LeaseResource("a different client", CurrentVersion, CurrentTime));
                SenderProbe.ExpectMsg<LeaseActor.LeaseTaken>();
            });
        }

        [Fact(DisplayName = "LeaseActor should heartbeat granted lease")]
        public void HeartBeatGrantedLease()
        {
            RunTest(() =>
            {
                AcquireLease();

                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            });
        }

        [Fact(DisplayName = "LeaseActor should remove lease from k8s when released")]
        public void RemoveLeaseWhenReleased()
        {
            RunTest(() =>
            {
                AcquireLease();
                UnderTest.Tell(LeaseActor.Release.Instance, Sender);
                UpdateProbe.ExpectMsg(("", CurrentVersion));
            });
        }

        [Fact(DisplayName = "LeaseActor should remove lease from k8s conflict during update but lease has removed")]
        public void RemoveLeaseConflictDuringUpdateRemoved()
        {
            RunTest(() =>
            {
                // "should not happen (TM)"
                AcquireLease();
                UnderTest.Tell(LeaseActor.Release.Instance, Sender);
                UpdateProbe.ExpectMsg(("", CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource(null, CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseReleased>();
            });
        }

        [Fact(DisplayName = "LeaseActor should remove lease from k8s conflict during update but lease taken by another")]
        public void RemoveLeaseConflictDuringUpdateTaken()
        {
            RunTest(() =>
            {
                // "should not happen (TM)"
                AcquireLease();
                UnderTest.Tell(LeaseActor.Release.Instance, Sender);
                UpdateProbe.ExpectMsg(("", CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource("another client", CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseReleased>();
            });
        }

        [Fact(DisplayName = "LeaseActor should remove lease from k8s failure")]
        public void RemoveLeaseFromK8SFailure()
        {
            RunTest(() =>
            {
                var failure = new LeaseException("Failed to communicate with API server");
                AcquireLease();
                UnderTest.Tell(LeaseActor.Release.Instance, Sender);
                UpdateProbe.ExpectMsg(("", CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(new Status.Failure(failure));
                SenderProbe.ExpectMsg<Status.Failure>().Cause.Should().Be(failure);
            });
        }

        [Fact(DisplayName = "LeaseActor should sets granted when granted")]
        public void SetsGrantedWhenGranted()
        {
            RunTest(() =>
            {
                Granted.Value.Should().BeFalse();
                AcquireLease();
                AwaitAssert(() =>
                {
                    Granted.Value.Should().BeTrue();
                });
            });
        }

        [Fact(DisplayName = "LeaseActor should sets granted when acquired and released")]
        public void SetsGrantedWhenAcquiredAndReleased()
        {
            RunTest(() =>
            {
                Granted.Value.Should().BeFalse();
                AcquireLease();
                AwaitAssert(() =>
                {
                    Granted.Value.Should().BeTrue();
                });
                ReleaseLease();
                AwaitAssert(() =>
                {
                    Granted.Value.Should().BeFalse();
                });
            });
        }

        [Fact(DisplayName = "Released lock should be acquire-able")]
        public void ReleasedLockShouldBeAbleToAcquire()
        {
            RunTest(() =>
            {
                AcquireLease();
                ReleaseLease();
                // Version from the previous lock so can skip the read of the resource unless the CAS fails
                AcquireLeaseWithoutRead(OwnerName);
            });
        }

        [Fact(DisplayName = "released lock acquired with new version")]
        public void ReleasedLockAcquiredWithNewVersion()
        {
            RunTest(() =>
            {
                AcquireLease();
                ReleaseLease();
                
                // Version from the previous lock so can skip the read of the resource unless the CAS fails
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                // Fail due to cas, version has moved on by 6 but no one owns the lock
                var failedVersion = (CurrentVersionCount + 6).ToString();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource(null, failedVersion, CurrentTime)));
                // Try again
                UpdateProbe.ExpectMsg((OwnerName, failedVersion));
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, failedVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
            });
        }

        [Fact(DisplayName = "heart beat conflict should set granted to false")]
        public void HearBeatConflictShouldSetGrantedToFalse()
        {
            RunTest(() =>
            {
                AcquireLease();
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource("I stole your lock", CurrentVersion, CurrentTime)));
                AwaitAssert(() =>
                {
                    Granted.Value.Should().BeFalse();
                });
            });
        }

        [Fact(DisplayName = "heartbeat conflict should call lease lost callback")]
        public void HeartBeatConflictShouldCallLeaseLostCallback()
        {
            RunTest(() =>
            {
                var callbackCalled = false;
                AcquireLease(e =>
                {
                    callbackCalled = true;
                });
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource("I stole your lock", CurrentVersion, CurrentTime)));
                AwaitAssert(() =>
                {
                    callbackCalled.Should().BeTrue();
                });
            });
        }

        [Fact(DisplayName = "heartbeat fail should set granted to false")]
        public void HeartBeatFailShouldSetGrantedToFalse()
        {
            RunTest(() =>
            {
                AcquireLease();
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                // With retry logic, multiple failures are needed to exhaust the TTL window
                HeartBeatFailure();
                Granted.Value.Should().BeFalse();
            });
        }

        [Fact(DisplayName = "heartbeat fail should call least lost callback")]
        public void HeartBeatFailShouldCallLeaseLostCallback()
        {
            RunTest(() =>
            {
                var failure = new LeaseException("Failed to communicate with API server");
                Exception? callbackCalled = null;
                AcquireLease(e =>
                {
                    callbackCalled = e;
                });
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                // With retry logic, drive failures until TTL expires and callback fires
                DriveHeartBeatFailures(failure);
                AwaitAssert(() =>
                {
                    callbackCalled.Should().Be(failure);
                });
            });
        }

        [Fact(DisplayName = "transient heartbeat failure should retry and stay granted")]
        public void TransientHeartBeatFailureShouldRetryAndStayGranted()
        {
            RunTest(() =>
            {
                AcquireLease();
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                // First heartbeat fails transiently
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                UpdateProbe.Reply(new Status.Failure(new LeaseException("Transient failure")));

                // Should still be granted - actor retries within TTL window
                Granted.Value.Should().BeTrue();

                // Retry heartbeat succeeds
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));

                // Should still be granted and heartbeating normally
                Granted.Value.Should().BeTrue();

                // Normal heartbeat continues
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            });
        }

        [Fact(DisplayName = "multiple transient heartbeat failures should recover on success")]
        public void MultipleTransientHeartBeatFailuresShouldRecover()
        {
            RunTest(() =>
            {
                AcquireLease();
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                // Multiple consecutive failures, all within TTL window
                for (var i = 0; i < 3; i++)
                {
                    UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                    UpdateProbe.Reply(new Status.Failure(new LeaseException($"Transient failure {i}")));
                    Granted.Value.Should().BeTrue();
                }

                // Recovery: next heartbeat succeeds
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));

                // Lease is still held
                Granted.Value.Should().BeTrue();
            });
        }

        [Fact(DisplayName = "heartbeat failure should not call lease lost callback during retry")]
        public void HeartBeatFailureShouldNotCallLeaseLostCallbackDuringRetry()
        {
            RunTest(() =>
            {
                var callbackCalled = false;
                AcquireLease(e =>
                {
                    callbackCalled = true;
                });
                ExpectHeartBeat();
                Granted.Value.Should().BeTrue();

                // Single transient failure within TTL
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                UpdateProbe.Reply(new Status.Failure(new LeaseException("Transient failure")));

                // Callback should NOT be called - TTL still valid
                callbackCalled.Should().BeFalse();
                Granted.Value.Should().BeTrue();

                // Recovery
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));

                callbackCalled.Should().BeFalse();
            });
        }

        /// <summary>
        /// Reproduces the self-conflict bug: a heartbeat PUT succeeds on the API server but times out
        /// on the client. The actor retries (per #3404 fix) with a stale version. The retry gets a CAS
        /// conflict (Left&lt;&gt;), but the conflict response shows the SAME owner — the node is fighting
        /// itself because its previous PUT actually succeeded and bumped the version.
        ///
        /// Current behavior (BUG): the actor unconditionally releases the lease on any heartbeat conflict.
        /// Expected behavior: the actor should recognize it still owns the lease, update the version,
        /// and stay in Granted state.
        /// </summary>
        [Fact(DisplayName = "Bug #3407: heartbeat self-conflict after timeout should stay granted")]
        public async Task HeartbeatSelfConflictAfterTimeoutShouldStayGranted()
        {
            await RunTestAsync(async () =>
            {
                await AcquireLeaseAsync();
                await ExpectHeartBeatAsync();
                Granted.Value.Should().BeTrue();

                // Step 1: Heartbeat fires, but PUT times out on client (server succeeded and bumped version)
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
                UpdateProbe.Reply(new Status.Failure(
                    new LeaseException("API server request timed out")));

                // Should still be granted — within TTL retry window (#3404 fix)
                Granted.Value.Should().BeTrue();

                // Step 2: Actor retries heartbeat with stale version (server already moved to next version)
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));

                // Step 3: Retry gets CAS conflict — but the owner is US (previous PUT succeeded)
                // The server version moved forward because our timed-out PUT actually went through
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));

                // BUG: Actor currently releases the lease here (lines 400-404)
                // EXPECTED: Actor should recognize it's still the owner and stay Granted
                Granted.Value.Should().BeTrue();

                // Step 4: Heartbeat should continue normally with the updated version
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));

                Granted.Value.Should().BeTrue();
            });
        }

        /// <summary>
        /// Verifies that the lease lost callback is NOT called when a heartbeat self-conflict occurs.
        /// The node still owns the lease — the conflict is only due to a stale version from a
        /// previously-timed-out-but-successful PUT.
        /// </summary>
        [Fact(DisplayName = "Bug #3407: heartbeat self-conflict should not call lease lost callback")]
        public async Task HeartbeatSelfConflictShouldNotCallLeaseLostCallback()
        {
            await RunTestAsync(async () =>
            {
                var callbackCalled = false;
                await AcquireLeaseAsync(e =>
                {
                    callbackCalled = true;
                });
                await ExpectHeartBeatAsync();
                Granted.Value.Should().BeTrue();

                // Heartbeat times out on client
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
                UpdateProbe.Reply(new Status.Failure(
                    new LeaseException("API server request timed out")));

                // Retry gets self-conflict
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));

                // Callback should NOT be called — we still own the lease
                callbackCalled.Should().BeFalse();
                Granted.Value.Should().BeTrue();
            });
        }

        /// <summary>
        /// Verifies that a genuine conflict (different owner) during heartbeat still correctly
        /// releases the lease. This ensures the self-conflict fix doesn't break the existing
        /// behavior for real conflicts.
        /// </summary>
        [Fact(DisplayName = "Bug #3407: heartbeat conflict with different owner after timeout should still release")]
        public async Task HeartbeatConflictWithDifferentOwnerAfterTimeoutShouldRelease()
        {
            await RunTestAsync(async () =>
            {
                await AcquireLeaseAsync();
                await ExpectHeartBeatAsync();
                Granted.Value.Should().BeTrue();

                // Heartbeat times out on client
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
                UpdateProbe.Reply(new Status.Failure(
                    new LeaseException("API server request timed out")));

                // Retry gets conflict with a DIFFERENT owner — lease was genuinely stolen
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource("another-node-stole-it", CurrentVersion, CurrentTime)));

                // Should release — this is a genuine conflict
                await AwaitAssertAsync(() =>
                {
                    Granted.Value.Should().BeFalse();
                });
            });
        }

        [Fact(DisplayName = "lock should be acquire-able after heart beat conflict")]
        public void LockShouldAcquireAfterHeartBeatConflict()
        {
            RunTest(() =>
            {
                AcquireLease();
                ExpectHeartBeat();
                HeartBeatConflict();
                AcquireLease();
            });
        }
        
        [Fact(DisplayName = "lock should be acquire-able after heart beat fail")]
        public void LockShouldAcquireAfterHeartBeatFail()
        {
            RunTest(() =>
            {
                AcquireLease();
                ExpectHeartBeat();
                HeartBeatFailure();
                AcquireLease();
            });
        }

        [Fact(Skip = "TODO: Not implemented yet")]
        public void LeaseAcquireInReadingState()
        {
            // TODO this could accumulate senders and reply to all, atm it'll log saying previous action hasn't finished
        }

        [Fact(DisplayName = "LeaseActor should return lease taken if conflict when updating lease")]
        public void ReturnLeaseTakenIfConflictWhenUpdatingLease()
        {
            RunTest(() =>
            {
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource("I stole your lock", CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseTaken>();
            });
        }

        /// <summary>
        /// Reproduces a split-brain bug: when a CAS conflict occurs during granting and the configmap
        /// has no owner, the LeaseActor sends LeaseAcquired to the caller BEFORE the retry write completes.
        /// If the retry then fails (another node steals the lease), the caller already believes it holds the
        /// lease — but it doesn't. localGranted is never set to true, heartbeat never starts.
        ///
        /// This test asserts the CORRECT behavior: the caller should receive LeaseTaken (not LeaseAcquired)
        /// when the retry write is stolen by another node.
        /// </summary>
        [Fact(DisplayName = "Bug #3402: should NOT send premature LeaseAcquired when conflict retry is stolen by another node")]
        public async Task ShouldNotSendPrematureLeaseAcquiredWhenConflictRetryIsStolen()
        {
            await RunTestAsync(async () =>
            {
                // Step 1: Send Acquire, complete the read (lease unowned), enter Granting state
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                await LeaseProbe.ExpectMsgAsync(LeaseName);
                LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));

                // Step 2: First write attempt — expect the CAS update
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));

                // Step 3: Reply with CAS conflict (412), but configmap has NO owner (version moved on)
                // This triggers the null-owner retry path at LeaseActor.cs line 359-368
                var conflictVersion = (CurrentVersionCount + 5).ToString();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource(null, conflictVersion, CurrentTime)));

                // Step 4: Retry write is dispatched — expect it with the new version
                await UpdateProbe.ExpectMsgAsync((OwnerName, conflictVersion));

                // Step 5: Retry FAILS — another node stole the lease between conflict and retry
                var stolenVersion = (CurrentVersionCount + 10).ToString();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource("another-node-stole-it", stolenVersion, CurrentTime)));

                // Step 6: Assert CORRECT behavior — caller should get LeaseTaken, NOT LeaseAcquired
                // BUG: Currently the caller receives LeaseAcquired (premature, from line 365)
                //       followed by LeaseTaken (which goes to dead letters since Ask already completed)
                await SenderProbe.ExpectMsgAsync<LeaseActor.LeaseTaken>();

                // Step 7: localGranted should be false — the lease was never actually acquired
                Granted.Value.Should().BeFalse();
            });
        }

        /// <summary>
        /// Verifies that when a CAS conflict occurs with no owner and the retry SUCCEEDS,
        /// the lease is properly granted with localGranted=true and heartbeat started.
        /// This is the happy-path counterpart to the split-brain test above.
        /// </summary>
        [Fact(DisplayName = "Bug #3402: should grant lease only after conflict retry succeeds")]
        public async Task ShouldGrantLeaseOnlyAfterConflictRetrySucceeds()
        {
            await RunTestAsync(async () =>
            {
                // Step 1: Send Acquire, complete the read (lease unowned), enter Granting state
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                await LeaseProbe.ExpectMsgAsync(LeaseName);
                LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));

                // Step 2: First write attempt
                await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));

                // Step 3: CAS conflict, no owner
                var conflictVersion = (CurrentVersionCount + 5).ToString();
                UpdateProbe.Reply(
                    new Left<LeaseResource, LeaseResource>(
                        new LeaseResource(null, conflictVersion, CurrentTime)));

                // Step 4: Retry dispatched
                await UpdateProbe.ExpectMsgAsync((OwnerName, conflictVersion));

                // Step 5: Retry SUCCEEDS
                var grantedVersion = (CurrentVersionCount + 11).ToString();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, grantedVersion, CurrentTime)));

                // Step 6: Now we should get LeaseAcquired
                await SenderProbe.ExpectMsgAsync<LeaseActor.LeaseAcquired>();

                // Step 7: localGranted should be TRUE — the lease was properly acquired
                await AwaitAssertAsync(() =>
                {
                    Granted.Value.Should().BeTrue();
                });
            });
        }

        [Fact(DisplayName = "LeaseActor should be able to get lease after failing previous grant update")]
        public void AbleToGetLeaseAfterFailingPreviousGrantUpdate()
        {
            RunTest(() =>
            {
                FailToGetLeaseDuringGrantingUpdate();
                AcquireLease();
            });
        }

        [Fact(DisplayName = "allow lease to be overwritten if TTL expired (from IDLE state, need version read)")]
        public void AllowLeaseOverwriteIfTtlExpiredInIdleState()
        {
            RunTest(() =>
            {
                var crashedClient = "crashedClient";
                
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                
                // lease is now older than the timeout
                LeaseProbe.Reply(new LeaseResource(
                    crashedClient,
                    CurrentVersion,
                    CurrentTime - LeaseSettings.TimeoutSettings.HeartbeatTimeout * 2));
                
                // try and get the lease
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
            });
        }

        [Fact(DisplayName = "LeaseActor should allow lease to be overwritten if TTL expired (after previous failed attempt)")]
        public void AllowLeaseOverwriteIfTtlExpiredAfterFail()
        {
            RunTest(() =>
            {
                const string crashedClient = "crashedClient";
                FailToGetTakenLease(crashedClient);
                
                // Second try the TTL is reached
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                
                // lease is now older than the timeout
                LeaseProbe.Reply(new LeaseResource(
                    crashedClient,
                    CurrentVersion,
                    CurrentTime - LeaseSettings.TimeoutSettings.HeartbeatTimeout * 2));

                // try and get the lease
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
            });
        }

        // If we crash and then come back and read our own client name back AND it hasn't timed out
        [Fact(DisplayName = "LeaseActor should allow lease to be taken if owned by same client name from IDLE")]
        public void AllowTakeLeaseIfOwnedBySameClientName()
        {
            RunTest(() =>
            {
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new LeaseResource(OwnerName, CurrentVersion, CurrentTime));
                UpdateProbe.ExpectNoMsg(LeaseSettings.TimeoutSettings.HeartbeatInterval / 2); // no time update required
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
                ExpectHeartBeat();
            });
        }
        
        // If we crash and read our own client name back and it has timed out it needs a time update
        // in this case another node could be trying to get the lease so we should go through
        // the full granting process
        [Fact(DisplayName = "LeaseActor should renew time if lease is owned by client on initial acquire")]
        public void RenewTimeIfLeaseIsOwnedByClientOnInitialAcquire()
        {
            RunTest(() =>
            {
                UnderTest.Tell(new LeaseActor.Acquire(), Sender);
                LeaseProbe.ExpectMsg(LeaseName);
                LeaseProbe.Reply(new LeaseResource(
                    OwnerName,
                    CurrentVersion,
                    CurrentTime - LeaseSettings.TimeoutSettings.HeartbeatTimeout * 2));
                SenderProbe.ExpectNoMsg(LeaseSettings.TimeoutSettings.HeartbeatTimeout / 3); // not granted yet
                UpdateProbe.ExpectMsg((OwnerName, CurrentVersion)); // Update time
                IncrementVersion();
                UpdateProbe.Reply(
                    new Right<LeaseResource, LeaseResource>(
                        new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
                SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
                ExpectHeartBeat();
            });
        }
    }
    
    internal class MockKubernetesApi : IKubernetesApi
    {
        private readonly IActorRef _currentLease;
        private readonly IActorRef _updateLease;
            
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        public MockKubernetesApi(IActorRef currentLease, IActorRef updateLease)
        {
            _currentLease = currentLease;
            _updateLease = updateLease;
        }
        
        public async Task<LeaseResource> ReadOrCreateLeaseResource(string name)
            => await _currentLease.Ask<LeaseResource>(name, _timeout);

        public async Task<Either<LeaseResource, LeaseResource>> UpdateLeaseResource(
            string leaseName,
            string ownerName,
            string version,
            DateTime? time = null)
            => await _updateLease.Ask<Either<LeaseResource, LeaseResource>>((ownerName, version), _timeout);
    }

    public abstract class LeaseActorTest: TestKit.Xunit.TestKit
    {
        protected const string LeaseName = "sbr";
        protected const string OwnerName = "owner1";

        private MockKubernetesApi? _mockKubernetesApi;

        protected int CurrentVersionCount { get; private set; } = 1;

        private TestProbe? _leaseProbe;
        protected TestProbe LeaseProbe => _leaseProbe ?? throw new Exception("Not initialized");

        private TestProbe? _updateProbe;
        protected TestProbe UpdateProbe => _updateProbe ?? throw new Exception("Not initialized");

        private AtomicBoolean? _granted;
        protected AtomicBoolean Granted => _granted ?? throw new Exception("Not initialized");

        private IActorRef? _underTest;
        protected IActorRef UnderTest => _underTest ?? throw new Exception("Not initialized");

        private TestProbe? _senderProbe;
        protected TestProbe SenderProbe => _senderProbe ?? throw new Exception("Not initialized");

        protected IActorRef Sender => _senderProbe?.Ref ?? throw new Exception("Not initialized");
        
        protected readonly LeaseSettings LeaseSettings;
        
        private static Config Config()
            => ConfigurationFactory.ParseString(@"
                akka.loglevel=DEBUG
                akka.stdout-loglevel=DEBUG
                akka.actor.debug.fsm=true
                akka.remote.dot-netty.tcp.port = 0")
                .WithFallback(Discovery.DiscoveryProvider.DefaultConfiguration())
                .WithFallback(KubernetesLease.DefaultConfiguration);

        protected LeaseActorTest(string testName, ITestOutputHelper output): base(Config(), testName, output)
        {
            LeaseSettings = new LeaseSettings(
                LeaseName,
                OwnerName,
                new TimeoutSettings(
                    TimeSpan.FromMilliseconds(25),
                    TimeSpan.FromMilliseconds(250),
                    TimeSpan.FromSeconds(1)),
                Configuration.Config.Empty);
        }

        protected async Task RunTestAsync(Func<Task> test)
        {
            Initialize();
            try
            {
                await test();
            }
            finally
            {
                Reset();
            }
        }
        
        protected void RunTest(Action test)
        {
            Initialize();
            try
            {
                test();
            }
            finally
            {
                Reset();
            }
        }

        private void Initialize()
        {
            _leaseProbe = CreateTestProbe();
            _updateProbe = CreateTestProbe();
            _mockKubernetesApi = new MockKubernetesApi(LeaseProbe.Ref, UpdateProbe.Ref);
            _granted = new AtomicBoolean();
            _senderProbe = CreateTestProbe();

            _underTest?.Tell(PoisonPill.Instance);
            _underTest = Sys.ActorOf(LeaseActor.Props(_mockKubernetesApi, LeaseSettings, LeaseName, Granted));
        }

        private void Reset()
        {
            _leaseProbe.Tell(PoisonPill.Instance);
            _updateProbe.Tell(PoisonPill.Instance);
            _senderProbe.Tell(PoisonPill.Instance);
            _underTest.Tell(PoisonPill.Instance);

            _leaseProbe = null;
            _updateProbe = null;
            _senderProbe = null;
            _underTest = null;
            _mockKubernetesApi = null;
            _granted = null;
        }

        protected string CurrentVersion => CurrentVersionCount.ToString();
        
        protected void IncrementVersion()
            => CurrentVersionCount++;

        protected DateTime CurrentTime => DateTime.UtcNow;

        protected void ExpectHeartBeat()
        {
            UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Right<LeaseResource, LeaseResource>(
                    new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
        }

        protected async Task ExpectHeartBeatAsync()
        {
            await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Right<LeaseResource, LeaseResource>(
                    new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
        }

        protected async Task AcquireLeaseAsync(Action<Exception?>? callback = null)
        {
            UnderTest.Tell(new LeaseActor.Acquire(callback), Sender);
            await LeaseProbe.ExpectMsgAsync(LeaseName);
            LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime.Date + 1.Milliseconds()));
            await UpdateProbe.ExpectMsgAsync((OwnerName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Right<LeaseResource, LeaseResource>(
                    new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
            await SenderProbe.ExpectMsgAsync<LeaseActor.LeaseAcquired>();
        }

        protected void FailToGetTakenLease(string leaseOwner)
        {
            UnderTest.Tell(new LeaseActor.Acquire(), Sender);
            LeaseProbe.ExpectMsg(LeaseName);
            LeaseProbe.Reply(new LeaseResource(leaseOwner, CurrentVersion, CurrentTime));
            SenderProbe.ExpectMsg<LeaseActor.LeaseTaken>();
        }

        protected void AcquireLease(Action<Exception?>? callback = null)
        {
            UnderTest.Tell(new LeaseActor.Acquire(callback), Sender);
            LeaseProbe.ExpectMsg(LeaseName);
            LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime.Date + 1.Milliseconds()));
            UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Right<LeaseResource, LeaseResource>(
                    new LeaseResource(OwnerName, CurrentVersion, CurrentTime)));
            SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
        }

        protected void AcquireLeaseWithoutRead(string clientName)
        {
            UnderTest.Tell(new LeaseActor.Acquire(), Sender);
            UpdateProbe.ExpectMsg((clientName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Right<LeaseResource, LeaseResource>(
                    new LeaseResource(clientName, CurrentVersion, CurrentTime)));
            SenderProbe.ExpectMsg<LeaseActor.LeaseAcquired>();
        }

        protected void ReleaseLease()
        {
            UnderTest.Tell(LeaseActor.Release.Instance, Sender);
            UpdateProbe.ExpectMsg(("", CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Right<LeaseResource, LeaseResource>(
                    new LeaseResource(null, CurrentVersion, CurrentTime)));
            SenderProbe.ExpectMsg<LeaseActor.LeaseReleased>();
        }

        protected void GoToGrantingFromIdle(string clientName)
        {
            UnderTest.Tell(new LeaseActor.Acquire(), Sender);
            IncrementVersion();
            LeaseProbe.ExpectMsg(LeaseName);
            LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));
            UpdateProbe.ExpectMsg((clientName, CurrentVersion));
        }

        protected void FailToGetLeaseDuringGrantingUpdate()
        {
            UnderTest.Tell(new LeaseActor.Acquire(), Sender);
            LeaseProbe.ExpectMsg(LeaseName);
            LeaseProbe.Reply(new LeaseResource(null, CurrentVersion, CurrentTime));
            UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Left<LeaseResource, LeaseResource>(
                    new LeaseResource("Someone else :(", CurrentVersion, CurrentTime)));
            SenderProbe.ExpectMsg<LeaseActor.LeaseTaken>();
        }

        protected void K8SApiFailureDuringRead()
        {
            var failure = new LeaseException("Failed to communicate with API server");
            UnderTest.Tell(new LeaseActor.Acquire(), Sender);
            LeaseProbe.ExpectMsg(LeaseName);
            LeaseProbe.Reply(new Status.Failure(failure));
            var receivedFailure = SenderProbe.ExpectMsg<Status.Failure>();
            receivedFailure.Cause.Should().Be(failure);
        }

        protected void HeartBeatConflict()
        {
            UpdateProbe.ExpectMsg((OwnerName, CurrentVersion));
            IncrementVersion();
            UpdateProbe.Reply(
                new Left<LeaseResource, LeaseResource>(
                    new LeaseResource("I stole your lock", CurrentVersion, CurrentTime)));
            AwaitAssert(() =>
            {
                Granted.Value.Should().BeFalse();
            });
        }

        protected void HeartBeatFailure()
        {
            DriveHeartBeatFailures(new LeaseException("Failed to communicate with API server"));
        }

        protected void DriveHeartBeatFailures(Exception failure)
        {
            while (Granted.Value)
            {
                UpdateProbe.ExpectMsg<(string, string)>(TimeSpan.FromSeconds(2));
                UpdateProbe.Reply(new Status.Failure(failure));
                Task.Delay(10).Wait();
            }
            AwaitAssert(() => Granted.Value.Should().BeFalse());
        }
    }
}
