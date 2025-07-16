
//-----------------------------------------------------------------------
// <copyright file="SimpleDnsCache.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.IO;
using Akka.Util;
using TResolved = Akka.Discovery.Dns.Internal.DnsProtocol.Message;
namespace Akka.Discovery.Dns.Internal;

/// <summary>
/// Interface for DNS caches that support periodic cleanup of expired entries.
/// </summary>
internal interface IPeriodicCacheCleanup
{
    /// <summary>
    /// Cleans up expired entries from the cache.
    /// </summary>
    void CleanUp();
}

/// <summary>
/// A simple in-memory DNS cache that stores resolved DNS entries with TTL-based expiration.
/// This class is a copy of SimpleDnsCache adjusted for DnsClient.Answer use 
/// </summary>
public class AsyncDnsCache : DnsBase, IPeriodicCacheCleanup
{
    private readonly AtomicReference<Cache> _cache;
    private readonly long _ticksBase;

    /// <summary>
    /// Initializes a new instance of the AsyncDnsCache.
    /// </summary>
    public AsyncDnsCache()
    {
        _cache = new AtomicReference<Cache>(new Cache(new SortedSet<ExpiryEntry>(new ExpiryEntryComparer()), new Dictionary<string, CacheEntry>(), Clock));
        _ticksBase = DateTime.Now.Ticks;
    }

    // /// <summary>
    // /// Gets a cached DNS resolution result for the specified hostname.
    // /// </summary>
    // /// <param name="name">The hostname to lookup in the cache.</param>
    // /// <returns>The cached DNS resolution result, or null if not found or expired.</returns>
    internal TResolved? GetCached(string name) => _cache.Value.Get(name);

    internal static IO.Dns.Resolved? Convert(TResolved? answer) =>
        answer == null ? null : 
        new(answer.FirstQuestionName, 
            TResolved.ToIpAddresses(answer, DnsProtocol.RecordType.A), 
            TResolved.ToIpAddresses(answer, DnsProtocol.RecordType.Aaaa)
            );
    

    /// <summary>
    /// Gets a cached DNS resolution result for the specified hostname.
    /// </summary>
    /// <param name="name">The hostname to lookup in the cache.</param>
    /// <returns>The cached DNS resolution result, or null if not found or expired.</returns>
    public override IO.Dns.Resolved? Cached(string name) => Convert(GetCached(name));

    /// <summary>
    /// Gets the current clock time in milliseconds since cache initialization.
    /// </summary>
    /// <returns>The current clock time in milliseconds.</returns>
    protected virtual long Clock()
    {
        var now = DateTime.Now.Ticks;
        return now - _ticksBase < 0
            ? 0
            : (now - _ticksBase) / 10000;
    }

    /// <summary>
    /// Adds a resolved DNS entry to the cache with the specified TTL.
    /// </summary>
    /// <param name="r">The resolved DNS entry to add to the cache.</param>
    /// <param name="ttl">Time-to-live in milliseconds for the entry.</param>
    internal void Put(TResolved r, long ttl)
    {
        var c = _cache.Value;
        if (!_cache.CompareAndSet(c, c.Put(r, ttl)))
            Put(r, ttl);
    }

    /// <summary>
    /// Cleans up expired entries from the cache.
    /// </summary>
    public void CleanUp()
    {
        var c = _cache.Value;
        if (!_cache.CompareAndSet(c, c.Cleanup()))
            CleanUp();
    }

    class Cache
    {
        private readonly SortedSet<ExpiryEntry> _queue;
        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly Func<long> _clock;
        private readonly object _queueCleanupLock = new();

        public Cache(SortedSet<ExpiryEntry> queue, Dictionary<string, CacheEntry> cache, Func<long> clock)
        {
            _queue = queue;
            _cache = cache;
            _clock = clock;
        }

        public TResolved? Get(string name)
        {
            if (_cache.TryGetValue(name, out var e) && e.IsValid(_clock()))
                return e.Answer;
            return null;
        }

        public Cache Put(TResolved answer, long ttl)
        {
            var until = _clock() + ttl;

            var cache = new Dictionary<string, CacheEntry>(_cache);

            cache[answer.FirstQuestionName] = new CacheEntry(answer, until);

            return new Cache(
                queue: new SortedSet<ExpiryEntry>(_queue, new ExpiryEntryComparer()) { new(answer.FirstQuestionName, until) },
                cache: cache,
                clock: _clock); 
        }

        public Cache Cleanup()
        {
            lock (_queueCleanupLock)
            {
                var now = _clock();
                while (_queue.Any() && !_queue.First().IsValid(now))
                {
                    var minEntry = _queue.First();
                    var name = minEntry.Name;
                    _queue.Remove(minEntry);

                    if (_cache.TryGetValue(name, out var cacheEntry) && !cacheEntry.IsValid(now))
                        _cache.Remove(name);
                }
            }
                
            return new Cache(new SortedSet<ExpiryEntry>(), new Dictionary<string, CacheEntry>(_cache), _clock);
        }
    }

    class CacheEntry
    {
        public CacheEntry(TResolved answer, long until)
        {
            Answer = answer;
            Until = until;
        }

        public TResolved Answer { get; private set; }
        public long Until { get; private set; }

        public bool IsValid(long clock)
        {
            return clock < Until;
        }
    }

    class ExpiryEntry 
    {
        public ExpiryEntry(string name, long until)
        {
            Name = name;
            Until = until;
        }

        public string Name { get; private set; }
        public long Until { get; private set; }

        public bool IsValid(long clock)
        {
            return clock < Until;
        }
    }

    class ExpiryEntryComparer : IComparer<ExpiryEntry>
    {
        /// <inheritdoc/>
        public int Compare(ExpiryEntry x, ExpiryEntry y)
        {
            return x.Until.CompareTo(y.Until);
        }
    }
}