using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityIsh.Core
{
    /// <summary>
    /// Represents one party-line channel (e.g. PL-A or PL-B).
    /// </summary>
    public sealed class PartyLine
    {
        public string Id { get; }
        public string Room { get; }
        public bool CanTalk { get; set; }
        public bool Listening { get; set; } = true;
        public bool IsTalking { get; private set; }

        public PartyLine(string id, string room, bool canTalk)
        {
            Id = id;
            Room = room;
            CanTalk = canTalk;
        }

        public void PttDown(Audio.ITransportService transport)
        {
            if (!CanTalk || IsTalking) return;
            IsTalking = true;
            transport.SetPublishing(Id, true);
        }

        public void PttUp(Audio.ITransportService transport)
        {
            if (!IsTalking) return;
            IsTalking = false;
            transport.SetPublishing(Id, false);
        }
    }

    /// <summary>
    /// Manages both party-line channels and routes PTT and listen commands
    /// to the underlying transport service.
    /// </summary>
    public sealed class PartyLineService
    {
        private readonly Dictionary<string, PartyLine> _lines = new();
        private readonly Audio.ITransportService _transport;

        public PartyLineService(Audio.ITransportService transport)
        {
            _transport = transport;
        }

        public void Register(string id, string room, bool canTalk)
            => _lines[id] = new PartyLine(id, room, canTalk);

        public PartyLine Get(string id)
            => _lines.TryGetValue(id, out var pl) ? pl : null;

        public IReadOnlyDictionary<string, PartyLine> All => _lines;

        /// <summary>Toggle listen state and apply it to the transport layer.</summary>
        public void SetListen(string id, bool listen)
        {
            if (!_lines.TryGetValue(id, out var pl)) return;
            pl.Listening = listen;
            _transport.SetSubscribeAll(id, listen);
            Debug.Log($"[PartyLineService] {id} listen={listen}");
        }

        public void PttDown(string id) => _lines.GetValueOrDefault(id)?.PttDown(_transport);
        public void PttUp(string id) => _lines.GetValueOrDefault(id)?.PttUp(_transport);
    }
}
