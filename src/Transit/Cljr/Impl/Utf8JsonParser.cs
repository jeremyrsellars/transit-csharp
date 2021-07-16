﻿using System.Collections.Immutable;
using Sellars.Transit.Alpha;
using Beerendonk.Transit.Impl;
using System.Threading;
using System;
using System.Text.Json;
using System.Buffers;
using System.Diagnostics;

namespace Sellars.Transit.Impl
{
    /// <summary>
    /// Represents a JSON parser.
    /// </summary>
    [DebuggerDisplay("{this}[CurrentToken={{BytesAsString}}]")]
    internal class Utf8JsonParser : AbstractParser
    {
        private readonly Utf8JsonStreamReader streamReader;
        private ReadOnlySequence<byte> bytes;
        private readonly JsonReaderOptions options;
        private JsonReaderState? initialState;

        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Utf8JsonParser"/> class.
        /// </summary>
        /// <param name="reader">The json text reader.</param>
        /// <param name="handlers">The handlers.</param>
        /// <param name="defaultHandler">The default handler.</param>
        /// <param name="dictionaryBuilder">The dictionary builder.</param>
        /// <param name="listBuilder">The list builder.</param>
        public Utf8JsonParser(
            System.IO.Stream input,
            JsonReaderOptions options,
            IImmutableDictionary<string, IReadHandler> handlers,
            IDefaultReadHandler<object> defaultHandler,
            IDictionaryReader dictionaryBuilder,
            IListReader listBuilder)
            : base(handlers, defaultHandler, dictionaryBuilder, listBuilder)
        {
            streamReader = new Utf8JsonStreamReader(input);
            this.options = options;
        }

        /// <summary>
        /// Parses the specified cache.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <returns></returns>
        public override object Parse(ReadCache cache)
        {
            if (!(this.initialState is JsonReaderState state))
            {
                // The initial state is nearly == default(JsonReaderState).
                // This is the only way to set the options, though.
                initialState = state = new Utf8JsonReader(ReadOnlySequence<byte>.Empty, this.options).CurrentState;
            }

            if (streamReader.ReadAsync(state, CancellationToken).Result is ReadOnlySequence<byte> bytes)
            {
                this.bytes = bytes;
                var rdr = new Utf8JsonReader(bytes, this.options);
                rdr.Read();
                switch (rdr.TokenType)
                {
                    case JsonTokenType.None: return ParseUnknown(ref rdr, options, cache);
                    case JsonTokenType.Number:
                        return ParseIntegerOrFloatNumber(cache, bytes, ref rdr);
                    case JsonTokenType.Null:
                        ReadToken(ref rdr);
                        return null;
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        return ParseBoolean(ref rdr, cache);
                    case JsonTokenType.String:
                        return cache.CacheRead(ParseString(ref rdr, cache), false, this);
                    case JsonTokenType.StartArray:
                        return ParseArray(ref rdr, options, false, cache, null);
                    case JsonTokenType.StartObject:
                        return ParseMap(ref rdr, options, false, cache, null, JsonTokenType.EndObject);
                    default:
                        return ParseUnknown(ref rdr, options, cache);
                }
            }
            return null;
        }

        private object ParseIntegerOrFloatNumber(ReadCache cache, ReadOnlySequence<byte> bytes, ref Utf8JsonReader rdr)
        {
            if (bytes.IsSingleSegment)
            {
                if (MemoryExtensions.LastIndexOf(bytes.First.Span, (byte)'.') >= 0)
                    return ParseFloat(ref rdr, cache);
            }
            else
            {
                foreach (var span in bytes)
                {
                    if (MemoryExtensions.LastIndexOf(span.Span, (byte)'.') >= 0)
                        return ParseFloat(ref rdr, cache);
                }
            }
            return ParseInteger(ref rdr, cache);
        }

        internal void ReadToken(ref Utf8JsonReader rdr, bool allowTokenUnavailable = true, JsonTokenType? expectedCurrentTokenType = default)
        {
            if (rdr.CurrentDepth == 0 && rdr.TokenType != JsonTokenType.StartArray && rdr.TokenType != JsonTokenType.StartObject)
                return;

            if (streamReader.ReadAsync(rdr.CurrentState, CancellationToken).Result is ReadOnlySequence<byte> bytes)
            {
                this.bytes = bytes;

                rdr = new Utf8JsonReader(bytes, false, rdr.CurrentState);
                var tokenAvailable = rdr.Read();

                if (!tokenAvailable && !allowTokenUnavailable)
                    throw new InvalidOperationException("No tokens available.");
            }
            else
            {
                if (allowTokenUnavailable)
                    return;
                throw new InvalidOperationException("No bytes available.");
            }
        }

        private string BytesAsString => System.Text.Encoding.UTF8.GetString(bytes.ToArray());

        internal static object ParseUnknown(ref Utf8JsonReader rdr, JsonReaderOptions options, ReadCache cache) =>
            throw new NotSupportedException($"Not supported/implemented.  Type: {rdr.TokenType}.");

        internal long ParseInteger(ref Utf8JsonReader rdr, ReadCache cache)
        {
            var v = rdr.GetInt64();
            ReadToken(ref rdr);
            return v;
        }

        internal bool ParseBoolean(ref Utf8JsonReader rdr, ReadCache cache)
        {
            var v = rdr.GetBoolean();
            ReadToken(ref rdr);
            return v;
        }

        internal double ParseFloat(ref Utf8JsonReader rdr, ReadCache cache)
        {
            var v = rdr.GetDouble();
            ReadToken(ref rdr);
            return v;
        }

        internal string ParseString(ref Utf8JsonReader rdr, ReadCache cache)
        {
            var v = rdr.GetString();
            ReadToken(ref rdr);
            return v;
        }

        internal object ParseArray(ref Utf8JsonReader rdr, JsonReaderOptions options, bool asDictionaryKey, ReadCache cache, IListReadHandler handler)
        {
            ReadToken(ref rdr, expectedCurrentTokenType: JsonTokenType.StartArray);
            if (rdr.TokenType != JsonTokenType.EndArray)
            {
                object firstVal = ParseVal(ref rdr, options, false, cache);
                if (firstVal != null)
                {
                    if (firstVal is string fvs && fvs == Constants.DirectoryAsList)
                    {
                        // if the same, build a map w/ rest of array contents
                        return ParseArrayAsDictionary(ref rdr, options, false, cache, dictionaryBuilder);
                    }
                    else if (firstVal is Tag)
                    {
                        object val;
                        string tag = ((Tag)firstVal).GetValue();
                        IReadHandler val_handler;
                        if (TryGetHandler(tag, out val_handler))
                        {
                            if (rdr.TokenType == JsonTokenType.StartObject && val_handler is IDictionaryReadHandler dictHandler)
                            {
                                // use map reader to decode value
                                val = ParseArrayAsDictionary(ref rdr, options, false, cache, dictHandler.DictionaryReader());
                            }
                            else if (rdr.TokenType == JsonTokenType.StartArray && val_handler is IListReadHandler listHandler)
                            {
                                // use array reader to decode value
                                val = ParseArray(ref rdr, options, false, cache, listHandler);
                            }
                            else
                            {
                                // read value and decode normally
                                val = val_handler.FromRepresentation(ParseVal(ref rdr, options, false, cache));
                            }
                        }
                        else
                        {
                            // default decode
                            val = this.Decode(tag, ParseVal(ref rdr, options, false, cache));
                        }
                        ReadToken(ref rdr, expectedCurrentTokenType: JsonTokenType.EndArray); // advance past end of object or array
                        return val;
                    }
                }

                // Process list w/o special decoding or interpretation
                IListReader lr = (handler != null) ? handler.ListReader() : listBuilder;
                object l = lr.Init();
                l = lr.Add(l, firstVal);
                while (rdr.TokenType != JsonTokenType.EndArray)
                {
                    l = lr.Add(l, ParseVal(ref rdr, options, false, cache));
                }
                ReadToken(ref rdr, expectedCurrentTokenType: JsonTokenType.EndArray); // advance past end of object or array
                return lr.Complete(l);
            }

            // Make an empty collection, honoring handler's ListReader, if present
            IListReader lr2 = (handler != null) ? handler.ListReader() : listBuilder;
            return lr2.Complete(lr2.Init());
        }

        internal object ParseMap(ref Utf8JsonReader rdr, JsonReaderOptions options, bool asDictionaryKey, ReadCache cache, IDictionaryReadHandler handler, JsonTokenType endToken)
        {
            IDictionaryReader dr = (handler != null) ? handler.DictionaryReader() : dictionaryBuilder;

            object d = dr.Init();

            ReadToken(ref rdr, expectedCurrentTokenType: JsonTokenType.StartObject);
            while (rdr.TokenType != endToken)
            {
                object key = ParseVal(ref rdr, options, true, cache);
                if (key is Tag)
                {
                    object val;
                    string tag = ((Tag)key).GetValue();
                    IReadHandler val_handler;
                    if (TryGetHandler(tag, out val_handler))
                    {
                        if (rdr.TokenType == JsonTokenType.StartObject && val_handler is IDictionaryReadHandler dictHandler)
                        {
                            // use map reader to decode value
                            val = ParseMap(ref rdr, options, false, cache, dictHandler, JsonTokenType.EndObject);
                        }
                        else if (rdr.TokenType == JsonTokenType.StartArray && val_handler is IListReadHandler listHandler)
                        {
                            // use array reader to decode value
                            val = ParseArray(ref rdr, options, false, cache, listHandler);
                        }
                        else
                        {
                            // read value and decode normally
                            val = val_handler.FromRepresentation(ParseVal(ref rdr, options, false, cache));
                        }
                    }
                    else
                    {
                        // default decode
                        val = this.Decode(tag, ParseVal(ref rdr, options, false, cache));
                    }
                    ReadToken(ref rdr, true, expectedCurrentTokenType: JsonTokenType.EndObject); // advance to read end of object or array

                    return val;
                }
                else
                {
                    d = dr.Add(d, key, ParseVal(ref rdr, options, false, cache));
                }
            }
            ReadToken(ref rdr, true, expectedCurrentTokenType: JsonTokenType.EndObject);

            return dr.Complete(d);
        }

        internal object ParseExtension(ref Utf8JsonReader rdr, JsonReaderOptions options, ReadCache cache) =>
            throw new NotSupportedException($"Not supported/implemented.  Type: {rdr.TokenType}.");

        /// <summary>
        /// Parses the value.
        /// </summary>
        /// <param name="asDictionaryKey">If set to <c>true</c> [as dictionary key].</param>
        /// <param name="cache">The cache.</param>
        /// <returns>
        /// The parsed value.
        /// </returns>
        public override object ParseVal(bool asDictionaryKey, ReadCache cache)
        {
            var initialState = new Utf8JsonReader(ReadOnlySequence<byte>.Empty, this.options).CurrentState;
            if (streamReader.ReadAsync(initialState, CancellationToken).Result is ReadOnlySequence<byte> bytes)
            {
                this.bytes = bytes;
                var rdr = new Utf8JsonReader(bytes);
                return ParseVal(ref rdr, options, asDictionaryKey, cache);
            }
            return null;
        }

        internal object ParseVal(ref Utf8JsonReader rdr, JsonReaderOptions options, bool asDictionaryKey, ReadCache cache)
        {
            switch (rdr.TokenType)
            {
                case JsonTokenType.Number:
                    return ParseIntegerOrFloatNumber(cache, bytes, ref rdr);
                case JsonTokenType.Null:
                    ReadToken(ref rdr);
                    return null;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return ParseBoolean(ref rdr, cache);
                case JsonTokenType.String:
                case JsonTokenType.PropertyName:
                    return cache.CacheRead(ParseString(ref rdr, cache), asDictionaryKey, this);
                case JsonTokenType.StartArray:
                    return ParseArray(ref rdr, options, asDictionaryKey, cache, null);
                case JsonTokenType.StartObject:
                    return ParseMap(ref rdr, options, asDictionaryKey, cache, null, JsonTokenType.EndObject);
                default:
                    return ParseUnknown(ref rdr, options, cache);
            }
        }

        /// <summary>
        /// Parses the dictionary.
        /// </summary>
        /// <param name="ignored">if set to <c>true</c> [ignored].</param>
        /// <param name="cache">The cache.</param>
        /// <param name="handler">The handler.</param>
        /// <returns></returns>
        public override object ParseDictionary(bool ignored, ReadCache cache, IDictionaryReadHandler handler)
        {
            throw new NotImplementedException($"Not implemented.");
        }

        private object ParseArrayAsDictionary(ref Utf8JsonReader rdr, JsonReaderOptions options, bool ignored, ReadCache cache, IDictionaryReader dictReader)
        {
            if (dictReader == null)
                throw new ArgumentNullException(nameof(dictReader));

            var dictionary = dictReader.Init();

            for (; rdr.TokenType != JsonTokenType.EndArray; )
            {
                var key = ParseVal(ref rdr, options, true, cache);
                var value = ParseVal(ref rdr, options, false, cache);
                dictionary = dictReader.Add(dictionary, key, value);
            }
            return dictReader.Complete(dictionary);
        }

        /// <summary>
        /// Parses the list.
        /// </summary>
        /// <param name="asDictionaryKey">If set to <c>true</c> [as dictionary key].</param>
        /// <param name="cache">The cache.</param>
        /// <param name="handler">The handler.</param>
        /// <returns>
        /// The parsed list.
        /// </returns>
        public override object ParseList(bool asDictionaryKey, ReadCache cache, IListReadHandler handler)
        {
            throw new NotImplementedException($"Not implemented.");
        }
    }
}