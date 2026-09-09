using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.Definition.UnitTests
{
    [TestClass]
    public class EnglishDictionaryProviderTests
    {
        [TestMethod]
        public async Task LookupAsync_should_translate_freedictionaryapi_response()
        {
            const string responseJson = """
                {
                  "word": "hello",
                  "entries": [{
                    "partOfSpeech": "interjection",
                    "pronunciations": [{ "type": "ipa", "text": "/həˈloʊ/", "tags": ["General American"] }],
                    "senses": [{
                      "definition": "A greeting.",
                      "examples": ["Hello, world."],
                      "synonyms": ["hi", "hi"],
                      "antonyms": [],
                      "subsenses": [{
                        "definition": "An expression of surprise.",
                        "examples": [],
                        "synonyms": [],
                        "antonyms": []
                      }]
                    }],
                    "synonyms": ["greeting"],
                    "antonyms": ["farewell"]
                  }],
                  "source": {
                    "url": "https://en.wiktionary.org/wiki/hello",
                    "license": { "name": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" }
                  }
                }
                """;

            var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseJson);
            var provider = new EnglishDictionaryProvider(new HttpClient(handler), "https://example.test/api/v1/entries/en/");

            var results = await provider.LookupAsync("hello", CancellationToken.None);

            Assert.AreEqual("https://example.test/api/v1/entries/en/hello", handler.RequestUri?.ToString());
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("hello", results[0].Word);
            Assert.AreEqual("/həˈloʊ/", results[0].Phonetic);
            Assert.AreEqual("CC BY-SA 4.0", results[0].License.Name);
            Assert.AreEqual("https://en.wiktionary.org/wiki/hello", results[0].SourceUrls.Single());
            Assert.AreEqual(2, results[0].Meanings.Single().Definitions.Count);
            Assert.AreEqual("Hello, world.", results[0].Meanings.Single().Definitions[0].Example);
            CollectionAssert.AreEqual(new[] { "hi" }, results[0].Meanings.Single().Definitions[0].Synonyms);
            CollectionAssert.AreEqual(new[] { "greeting" }, results[0].Meanings.Single().Synonyms);
            CollectionAssert.AreEqual(new[] { "farewell" }, results[0].Meanings.Single().Antonyms);
        }

        [TestMethod]
        public async Task LookupAsync_should_return_no_results_for_empty_entries()
        {
            const string responseJson = """
                {
                  "word": "notaword",
                  "entries": [],
                  "source": {
                    "url": "https://en.wiktionary.org",
                    "license": { "name": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" }
                  }
                }
                """;
            var provider = new EnglishDictionaryProvider(
                new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, responseJson)),
                "https://example.test/entries/en/");

            var results = await provider.LookupAsync("notaword", CancellationToken.None);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public async Task LookupAsync_should_remain_compatible_with_legacy_response_shape()
        {
            const string responseJson = """
                [{
                  "word": "hello",
                  "phonetic": "/həˈloʊ/",
                  "meanings": [{
                    "partOfSpeech": "interjection",
                    "definitions": [{ "definition": "A greeting." }]
                  }]
                }]
                """;
            var provider = new EnglishDictionaryProvider(
                new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, responseJson)),
                "https://legacy.example.test/entries/en/");

            var results = await provider.LookupAsync("hello", CancellationToken.None);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("A greeting.", results[0].Meanings.Single().Definitions.Single().Definition);
        }

        [TestMethod]
        public async Task LookupAsync_should_fall_back_to_datamuse_when_primary_fails()
        {
            var handler = new RoutingHttpMessageHandler(request =>
            {
                if (request.RequestUri.Host == "primary.example.test")
                {
                    return new HttpResponseMessage(HttpStatusCode.BadGateway)
                    {
                        Content = new StringContent("upstream unavailable")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "[{\"word\":\"hello\",\"defs\":[\"n\\tA greeting.\",\"v\\tTo greet someone.\"]}]",
                        Encoding.UTF8,
                        "application/json")
                };
            });
            var provider = new EnglishDictionaryProvider(
                new HttpClient(handler),
                "https://primary.example.test/entries/en/",
                "https://fallback.example.test/words?sp=");

            var results = await provider.LookupAsync("hello", CancellationToken.None);

            Assert.AreEqual(2, handler.RequestUris.Count);
            Assert.AreEqual("https://fallback.example.test/words?sp=hello&md=d&max=1", handler.RequestUris[1].ToString());
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Meanings.Any(meaning => meaning.PartOfSpeech == "noun"
                && meaning.Definitions.Any(definition => definition.Definition == "A greeting.")));
            Assert.IsTrue(results[0].Meanings.Any(meaning => meaning.PartOfSpeech == "verb"
                && meaning.Definitions.Any(definition => definition.Definition == "To greet someone.")));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("https://api.dictionaryapi.dev/api/v2/entries/en/")]
        [DataRow("https://api.dictionaryapi.dev/api/v2/entries/en")]
        public void NormalizeEnglishApiEndpoint_should_migrate_unusable_default(string endpoint)
        {
            Assert.AreEqual(
                PluginConfiguration.DefaultEnglishApiEndpoint,
                ConfigurationManager.NormalizeEnglishApiEndpoint(endpoint));
        }

        [TestMethod]
        public void NormalizeEnglishApiEndpoint_should_preserve_custom_endpoint()
        {
            const string endpoint = "https://dictionary.example.test/entries/en/";

            Assert.AreEqual(endpoint, ConfigurationManager.NormalizeEnglishApiEndpoint(endpoint));
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _responseJson;

            public StubHttpMessageHandler(HttpStatusCode statusCode, string responseJson)
            {
                _statusCode = statusCode;
                _responseJson = responseJson;
            }

            public System.Uri RequestUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class RoutingHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            public RoutingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            public List<System.Uri> RequestUris { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUris.Add(request.RequestUri);
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
