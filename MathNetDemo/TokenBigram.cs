



using System.Text;
using MathNet.Numerics.LinearAlgebra.Single;  // For Single precision (float)

using VectorF = MathNet.Numerics.LinearAlgebra.Vector<float>;  // Alias for Vector<float>

public class BigramEntry
{
    private static int maxCount = 10000; // Maximum number of tokens we can have
    public Dictionary<int, int> TokenCounts; // For this token, we have an associated token and token count

    public void AddCount(int nextTokId)
    {
        if(TokenCounts.ContainsKey(nextTokId))
        {
            if(TokenCounts[nextTokId] < maxCount)
                TokenCounts[nextTokId]++;
        }
        else
        {
            TokenCounts.Add(nextTokId, 1);
        }
    }

    public void AddTotalCount(int nextTokId, int totalCount)
    {
        TokenCounts[nextTokId] = totalCount;
    }

    public string LineToString()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var token in TokenCounts)
        {
            sb.Append(token.Key + " " + token.Value + " | ");
        }
        return sb.ToString();
    }

    public void LoadFromString(string line)
    {
        TokenCounts = new ();
        string[] parts = line.Split('|');
        foreach (var part in parts)
        {
            string tpart = part.Trim();
            //Console.WriteLine($"Part: >{part}<");
            string[] subparts = tpart.Split(' ');
            if(subparts.Length == 2)
            {
                //Console.WriteLine($"Subparts: >{subparts[0]}< >{subparts[1]}<");
                int tokId    = int.Parse(subparts[0]);
                int tokCount = int.Parse(subparts[1]);

                //Console.WriteLine($"Subparts ID: >{tokId}< >{tokCount}<");
                //TokenCounts[tokId] = tokCount;
                TokenCounts.Add(tokId, tokCount);
            }
        }
    }
}

public class TokenBigram
{
    public List<BigramEntry> BigramList = new List<BigramEntry>();

    // --------------------------------------------------------------------------------------------
    // MARK: Constructors
    // --------------------------------------------------------------------------------------------

    public TokenBigram(int tokenCount)
    {
        SetupBlankList(tokenCount);
    }

    public void SetupBlankList(int tokenCount)
    {
        for(int i = 0; i < tokenCount; i++)
        {
            BigramEntry entry = new BigramEntry();
            entry.TokenCounts = new ();
            BigramList.Add(entry);
        }
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Add Entry
    // --------------------------------------------------------------------------------------------

    public void AddTokenLink(int tokID1, int tokID2)
    {
        // Get the current token
        BigramEntry entry = BigramList[tokID1];

        entry.AddCount(tokID2);
    }

    public void AddTokenLinks(List<int> tokIdList)
    {
        for(int i = 0; i < tokIdList.Count - 1; i++)
        {
            AddTokenLink(tokIdList[i], tokIdList[i + 1]);
        }
    }

    public void ClearLinksForToken(int tokID)
    {
        BigramList[tokID].TokenCounts.Clear();
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Outputs
    // --------------------------------------------------------------------------------------------

    public VectorF GetBigramVector(int tokenID)
    {
        BigramEntry entry = BigramList[tokenID];

        VectorF retVec = VectorF.Build.Dense(BigramList.Count);

        foreach (var token in entry.TokenCounts)
        {
            retVec[token.Key] = token.Value;
        }

        return retVec;
    }

    public VectorF GetNormalizedBigramVector(int tokenID)
    {
        BigramEntry entry = BigramList[tokenID];
        VectorF countVec = VectorF.Build.Dense(BigramList.Count);
        int totalCount = 0;
        foreach (var token in entry.TokenCounts)
        {
            countVec[token.Key] = token.Value;
            totalCount += token.Value;
        }
        if(totalCount > 0)
            countVec = countVec.Divide(totalCount);
        return countVec;
    }

    // Get the next most likely token, reading the bigram values and returning the highest
    public int GetNextTokenId(int tokenID)
    {
        BigramEntry entry = BigramList[tokenID];
        int maxToken = -1;
        int maxCount = 0;

        //Console.WriteLine($"Entry: {entry.TokenCounts.Count}");

        foreach (var token in entry.TokenCounts)
        {
            if(token.Value > maxCount)
            {
                maxCount = token.Value;
                maxToken = token.Key;
            }
        }
        return maxToken;
    }

    // Return the total number of token associations
    public int GetNumAssociations()
    {
        int accumulatedCount = 0;

        foreach (var entry in BigramList)
            accumulatedCount += entry.TokenCounts.Count;

        return accumulatedCount;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Serialise
    // --------------------------------------------------------------------------------------------

    public void SaveToFile(string filepath)
    {
        using (var writer = new StreamWriter(filepath, false, Encoding.UTF8))
        {
            writer.WriteLine(BigramList.Count);
            int listIndex = 0;
            for(listIndex = 0; listIndex < BigramList.Count; listIndex++)
            {
                writer.WriteLine($"{listIndex} : {BigramList[listIndex].LineToString()}");
            }
        }
    }

    public static TokenBigram LoadFromFile(string filePath)
    {
        using (var reader = new StreamReader(filePath, Encoding.UTF8))
        {
            // Read the token count
            string? countline = reader.ReadLine();
            int tokenCount = int.Parse(countline!);

            TokenBigram retBigram = new TokenBigram(tokenCount);

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line == null)
                {
                    continue;
                }

                // Split off the token index
                string[] subparts = line.Split(':');
                if(subparts.Length == 2)
                {
                    //Console.WriteLine($"Parts: >{subparts[0]}< >{subparts[1]}<");;
                    int tokIndex = int.Parse(subparts[0]);
                    string tokLine = subparts[1];

                    tokLine = tokLine.Trim();

                    if (!String.IsNullOrEmpty(tokLine))
                        retBigram.BigramList[tokIndex].LoadFromString(tokLine);
                }
            }


            Console.WriteLine($"Loaded Bigram with {retBigram.GetNumAssociations()} associations");

            return retBigram;
        }
    }

}