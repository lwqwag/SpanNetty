﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static HttpVersion;

    public class WebSocketServerHandshaker13Test : WebSocketServerHandshakerTest
    {
        protected override WebSocketServerHandshaker NewHandshaker(string webSocketURL, string subprotocols, WebSocketDecoderConfig decoderConfig)
        {
            return new WebSocketServerHandshaker13(webSocketURL, subprotocols, decoderConfig);
        }

        protected override WebSocketVersion WebSocketVersion()
        {
            return Http.WebSockets.WebSocketVersion.V13;
        }

        [Fact]
        public void PerformOpeningHandshake() => PerformOpeningHandshake0(true);

        [Fact]
        public void PerformOpeningHandshakeSubProtocolNotSupported() => PerformOpeningHandshake0(false);

        private static void PerformOpeningHandshake0(bool subProtocol)
        {
            EmbeddedChannel ch = new EmbeddedChannel(
                    new HttpObjectAggregator(42), new HttpResponseEncoder(), new HttpRequestDecoder());

            if (subProtocol)
            {
                Upgrade0(ch, new WebSocketServerHandshaker13(
                        "ws://example.com/chat", "chat", false, int.MaxValue, false));
            }
            else
            {
                Upgrade0(ch, new WebSocketServerHandshaker13(
                        "ws://example.com/chat", null, false, int.MaxValue, false));
            }
            Assert.False(ch.Finish());
        }

        [Fact]
        public void CloseReasonWithEncoderAndDecoder()
        {
            CloseReason0(new HttpResponseEncoder(), new HttpRequestDecoder());
        }

        [Fact]
        public void CloseReasonWithCodec()
        {
            CloseReason0(new HttpServerCodec());
        }

        private static void CloseReason0(params IChannelHandler[] handlers)
        {
            EmbeddedChannel ch = new EmbeddedChannel(
                    new HttpObjectAggregator(42));
            ch.Pipeline.AddLast(handlers);
            Upgrade0(ch, new WebSocketServerHandshaker13("ws://example.com/chat", "chat",
                    WebSocketDecoderConfig.NewBuilder().MaxFramePayloadLength(4).CloseOnProtocolViolation(true).Build()));

            ch.WriteOutbound(new BinaryWebSocketFrame(Unpooled.WrappedBuffer(new byte[8])));
            var buffer = ch.ReadOutbound<IByteBuffer>();
            try
            {
                ch.WriteInbound(buffer);
                Assert.False(true);
            }
            catch (CorruptedWebSocketFrameException)
            {
                // expected
            }
            IReferenceCounted closeMessage = ch.ReadOutbound<IReferenceCounted>();
            Assert.True(closeMessage is IByteBuffer);
            closeMessage.Release();
            Assert.False(ch.Finish());
        }

        private static void Upgrade0(EmbeddedChannel ch, WebSocketServerHandshaker13 handshaker)
        {
            var req = new DefaultFullHttpRequest(Http11, HttpMethod.Get, "/chat");
            req.Headers.Set(HttpHeaderNames.Host, "server.example.com");
            req.Headers.Set(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket);
            req.Headers.Set(HttpHeaderNames.Connection, "Upgrade");
            req.Headers.Set(HttpHeaderNames.SecWebsocketKey, "dGhlIHNhbXBsZSBub25jZQ==");
            req.Headers.Set(HttpHeaderNames.SecWebsocketOrigin, "http://example.com");
            req.Headers.Set(HttpHeaderNames.SecWebsocketProtocol, "chat, superchat");
            req.Headers.Set(HttpHeaderNames.SecWebsocketVersion, "13");

            Assert.True(handshaker.HandshakeAsync(ch, req).Wait(TimeSpan.FromSeconds(2)));

            var resBuf = ch.ReadOutbound<IByteBuffer>();

            var ch2 = new EmbeddedChannel(new HttpResponseDecoder());
            ch2.WriteInbound(resBuf);
            var res = ch2.ReadInbound<IHttpResponse>();

            Assert.True(res.Headers.TryGet(HttpHeaderNames.SecWebsocketAccept, out ICharSequence value));
            Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", value.ToString());
            var subProtocols = handshaker.Subprotocols();
            if (subProtocols.Any())
            {
                Assert.True(res.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out value));
                Assert.Equal("chat", value.ToString());
            }
            else
            {
                Assert.False(res.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out _));
            }
            ReferenceCountUtil.Release(res);
            req.Release();
        }
    }
}
