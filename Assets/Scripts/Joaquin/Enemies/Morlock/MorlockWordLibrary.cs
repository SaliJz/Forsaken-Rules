using System.Collections.Generic;
using UnityEngine;

public class MorlockWordLibrary : MonoBehaviour
{
    [SerializeField] private List<string> words = new List<string> { "STATIC" };
    [SerializeField] private string fallbackWord = "STATIC";

    public string GetRandomWord()
    {
        string selectedWord = string.Empty;
        int validWordCount = 0;

        for (int index = 0; index < words.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(words[index]))
            {
                continue;
            }

            validWordCount++;

            if (Random.Range(0, validWordCount) == 0)
            {
                selectedWord = words[index].Trim();
            }
        }

        if (!string.IsNullOrEmpty(selectedWord))
        {
            return selectedWord;
        }

        return string.IsNullOrWhiteSpace(fallbackWord) ? "STATIC" : fallbackWord.Trim();
    }
}
