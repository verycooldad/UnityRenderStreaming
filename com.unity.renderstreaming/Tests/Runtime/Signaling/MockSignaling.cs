using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.RenderStreaming.Signaling;
using Unity.WebRTC;
using UnityEngine;

namespace Unity.RenderStreaming.RuntimeTest.Signaling
{
    public class MockSignaling : ISignaling
    {
        interface IMockSignalingManager
        {
            Task Add(MockSignaling signaling);
            Task Remove(MockSignaling signaling);
            Task OpenConnection(MockSignaling signaling, string connectionId);
            Task CloseConnection(MockSignaling signaling, string connectionId);
            Task Offer(MockSignaling owner, DescData data);
            Task Answer(MockSignaling owner, DescData data);
            Task Candidate(MockSignaling owner, CandidateData data);
        }

        class MockPublicSignalingManager : IMockSignalingManager
        {
            private List<MockSignaling> list = new List<MockSignaling>();
            private const int MillisecondsDelay = 10;

            public async Task Add(MockSignaling signaling)
            {
                await Task.Delay(MillisecondsDelay);
                list.Add(signaling);
                signaling.OnStart?.Invoke(signaling);
            }
            public async Task Remove(MockSignaling signaling)
            {
                await Task.Delay(MillisecondsDelay);
                list.Remove(signaling);
            }

            public async Task OpenConnection(MockSignaling signaling, string connectionId)
            {
                await Task.Delay(MillisecondsDelay);
                signaling.OnCreateConnection?.Invoke(signaling, connectionId, false);
            }

            public async Task CloseConnection(MockSignaling signaling, string connectionId)
            {
                await Task.Delay(MillisecondsDelay);
                signaling.OnDestroyConnection?.Invoke(signaling, connectionId);
            }
            public async Task Offer(MockSignaling owner, DescData data)
            {
                await Task.Delay(MillisecondsDelay);
                foreach (var signaling in list.Where(e => e != owner))
                {
                    signaling.OnOffer?.Invoke(signaling, data);
                }
            }

            public async Task Answer(MockSignaling owner, DescData data)
            {
                await Task.Delay(MillisecondsDelay);
                foreach (var signaling in list.Where(e => e != owner))
                {
                    signaling.OnAnswer?.Invoke(signaling, data);
                }
            }

            public async Task Candidate(MockSignaling owner, CandidateData data)
            {
                await Task.Delay(MillisecondsDelay);
                foreach (var signaling in list.Where(e => e != owner))
                {
                    signaling.OnIceCandidate?.Invoke(signaling, data);
                }
            }
        }

        class MockPrivateSignalingManager : IMockSignalingManager
        {
            private Dictionary<string, List<MockSignaling>> connectionIds = new Dictionary<string, List<MockSignaling>>();
            private const int MillisecondsDelay = 10;

            public async Task Add(MockSignaling signaling)
            {
                await Task.Delay(MillisecondsDelay);
                signaling.OnStart?.Invoke(signaling);
            }
            public async Task Remove(MockSignaling signaling)
            {
                await Task.Delay(MillisecondsDelay);
            }

            public async Task OpenConnection(MockSignaling signaling, string connectionId)
            {
                await Task.Delay(MillisecondsDelay);
                bool peerExists = connectionIds.TryGetValue(connectionId, out var list);
                if(!peerExists)
                {
                    list = new List<MockSignaling>();
                    connectionIds.Add(connectionId, list);
                }
                list.Add(signaling);
                signaling.OnCreateConnection?.Invoke(signaling, connectionId, peerExists);
            }

            public async Task CloseConnection(MockSignaling signaling, string connectionId)
            {
                await Task.Delay(MillisecondsDelay);
                bool peerExists = connectionIds.TryGetValue(connectionId, out var list);
                if (!peerExists || !list.Contains(signaling))
                {
                    Debug.LogError($"{connectionId} This connection id is not used.");
                }
                list.Remove(signaling);
                if (list.Count == 0)
                {
                    connectionIds.Remove(connectionId);
                }
                signaling.OnDestroyConnection?.Invoke(signaling, connectionId);
            }

            List<MockSignaling> FindList(MockSignaling owner, string connectionId)
            {
                if (!connectionIds.TryGetValue(connectionId, out var list))
                {
                    return null;
                }
                list = list.Where(e => e != owner).ToList();
                if (list.Count == 0)
                {
                    return null;
                }
                return list;
            }

            public async Task Offer(MockSignaling owner, DescData data)
            {
                await Task.Delay(MillisecondsDelay);
                var list = FindList(owner, data.connectionId);
                if (list == null)
                {
                    Debug.LogError($"{data.connectionId} This connection id is not ready other session.");
                    return;
                }
                foreach (var signaling in list)
                {
                    signaling.OnOffer?.Invoke(signaling, data);
                }
            }

            public async Task Answer(MockSignaling owner, DescData data)
            {
                await Task.Delay(MillisecondsDelay);
                var list = FindList(owner, data.connectionId);
                if (list == null)
                {
                    Debug.LogError($"{data.connectionId} This connection id is not ready other session.");
                    return;
                }
                foreach (var signaling in list)
                {
                    signaling.OnAnswer?.Invoke(signaling, data);
                }
            }

            public async Task Candidate(MockSignaling owner, CandidateData data)
            {
                await Task.Delay(MillisecondsDelay);
                var list = FindList(owner, data.connectionId);
                if (list == null)
                {
                    Debug.LogError($"{data.connectionId} This connection id is not ready other session.");
                    return;
                }
                foreach (var signaling in list.Where(e => e != owner))
                {
                    signaling.OnIceCandidate?.Invoke(signaling, data);
                }
            }
        }

        private static IMockSignalingManager manager = null;

        public static void Reset(bool enablePrivateMode)
        {
            if (enablePrivateMode)
            {
                manager = new MockPrivateSignalingManager();
            }
            else
            {
                manager = new MockPublicSignalingManager();
            }
        }

        static MockSignaling()
        {
            manager = new MockPublicSignalingManager();
        }

        public void Start()
        {
            manager.Add(this);
        }

        public void Stop()
        {
            manager.Remove(this);
        }

        public event OnStartHandler OnStart;
        public event OnConnectHandler OnCreateConnection;
        public event OnDisconnectHandler OnDestroyConnection;
        public event OnOfferHandler OnOffer;
        public event OnAnswerHandler OnAnswer;
        public event OnIceCandidateHandler OnIceCandidate;

        public void OpenConnection(string connectionId)
        {
            manager.OpenConnection(this, connectionId);
        }

        public void CloseConnection(string connectionId)
        {
            manager.CloseConnection(this, connectionId);
        }

        public void SendOffer(string connectionId, RTCSessionDescription offer)
        {
            DescData data = new DescData
            {
                connectionId = connectionId,
                type = "offer",
                sdp = offer.sdp
            };
            manager.Offer(this, data);
        }

        public void SendAnswer(string connectionId, RTCSessionDescription answer)
        {
            DescData data = new DescData
            {
                connectionId = connectionId,
                type = "answer",
                sdp = answer.sdp
            };
            manager.Answer(this, data);
        }

        public void SendCandidate(string connectionId, RTCIceCandidate candidate)
        {
            CandidateData data = new CandidateData
            {
                connectionId = connectionId,
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex.GetValueOrDefault()
            };
            manager.Candidate(this, data);
        }
    }
}
