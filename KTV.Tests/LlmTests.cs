using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KTV.Tests
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public MockHttpMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent)
            };
            return Task.FromResult(response);
        }
    }

    public class LlmTests
    {
        [Fact]
        public async Task AnalyzeIntentAsync_ParsesValidJsonResponse()
        {
            // Arrange
            string ollamaMockResponse = @"{
                ""model"": ""gemma2b"",
                ""response"": ""{\""Action\"": \""Search\"", \""Title\"": \""光年之外\"", \""Artist\"": \""鄧紫棋\""}""
            }";

            var handler = new MockHttpMessageHandler(ollamaMockResponse);
            var httpClient = new HttpClient(handler);
            var service = new LlmAgentService(null, httpClient);

            // Act
            var intent = await service.AnalyzeIntentAsync("我想點鄧紫棋的光年之外");

            // Assert
            Assert.NotNull(intent);
            Assert.Equal("Search", intent.Action);
            Assert.Equal("光年之外", intent.Title);
            Assert.Equal("鄧紫棋", intent.Artist);
        }

        [Fact]
        public async Task AnalyzeIntentAsync_ThrowsOnFailureStatusCode()
        {
            // Arrange
            var customHandler = new AdHocHttpMessageHandler(() => 
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            
            var httpClient = new HttpClient(customHandler);
            var service = new LlmAgentService(null, httpClient);

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(() => service.AnalyzeIntentAsync("fail request"));
        }
    }

    public class AdHocHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<Task<HttpResponseMessage>> _sendAsyncFunc;

        public AdHocHttpMessageHandler(Func<Task<HttpResponseMessage>> sendAsyncFunc)
        {
            _sendAsyncFunc = sendAsyncFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsyncFunc();
        }
    }
}
