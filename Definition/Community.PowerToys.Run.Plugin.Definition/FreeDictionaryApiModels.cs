using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.Definition
{
    internal class FreeDictionaryApiResponse
    {
        public string Word { get; set; }
        public List<FreeDictionaryEntry> Entries { get; set; } = new();
        public FreeDictionarySource Source { get; set; }
    }

    internal class FreeDictionaryEntry
    {
        public string PartOfSpeech { get; set; }
        public List<FreeDictionaryPronunciation> Pronunciations { get; set; } = new();
        public List<FreeDictionarySense> Senses { get; set; } = new();
        public List<string> Synonyms { get; set; } = new();
        public List<string> Antonyms { get; set; } = new();
    }

    internal class FreeDictionaryPronunciation
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    internal class FreeDictionarySense
    {
        public string Definition { get; set; }
        public List<string> Examples { get; set; } = new();
        public List<string> Synonyms { get; set; } = new();
        public List<string> Antonyms { get; set; } = new();
        public List<FreeDictionarySense> Subsenses { get; set; } = new();
    }

    internal class FreeDictionarySource
    {
        public string Url { get; set; }
        public FreeDictionaryLicense License { get; set; }
    }

    internal class FreeDictionaryLicense
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    internal class DatamuseDefinitionResponse
    {
        public string Word { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("defs")]
        public List<string> Definitions { get; set; } = new();
    }
}
