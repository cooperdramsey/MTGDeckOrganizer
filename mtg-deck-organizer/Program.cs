using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using MtgApiManager.Lib.Service;
using MtgApiManager.Lib.Model;
using MtgApiManager.Lib.Core;

namespace mtg_deck_organizer
{
    class Program
    {
        static void Main()
        {
            var deckList = @"deck_list.txt";
            string line;
            var cardNames = new List<string>();
            using (var file = new StreamReader(deckList))
            {
                var expr = @"^\d*\s";
                while((line = file.ReadLine()) != null)
                {
                    cardNames.Add(Regex.Replace(line, expr, string.Empty));
                }
            }

            //Gather all card sets
            var setService = new SetService();
            var setsAwaiter = setService.AllAsync();

            var tasks = new List<Task<Exceptional<List<Card>>>>();
            var service = new CardService();
            //Async gather all of the card details
            foreach (var name in cardNames)
            {
                tasks.Add(service
                    .Where(x => x.Name, name)
                    .AllAsync());
            }

            Task.WaitAll(tasks.ToArray());
            var sets = setsAwaiter.Result.Value.ToDictionary(x => x.Code, y => y.Name);

            var cards = new Dictionary<string, List<Card>>();
            foreach(var task in tasks)
            {
                cards.Add(task.Result.Value.First().Name, 
                    task.Result.Value
                    .Where(x => x.Set[0] != 'P' && x.Set[0].ToString() + x.Set[1].ToString() != "WC")
                    .ToList()
                    );
            }

            //Count the number of sets within the pulled cards
            var setCounts = GetSetCounts(cards, sets);

            var output = new Dictionary<string, List<Card>>();

            foreach (var item in cards)
            {
                foreach (var set in setCounts)
                {
                    if (item.Value.Where(x => x.Set == set.Key).Any())
                    {
                        if (output.ContainsKey(set.Key))
                        {
                            output[set.Key].Add(item.Value.Where(x => x.Set == set.Key).First());
                        }
                        else
                        {
                            output[set.Key] = new List<Card>(new[] { item.Value.Where(x => x.Set == set.Key).First() });
                        }
                        
                        break;
                    }
                }
            }

            foreach (var item in output)
            {
                Console.WriteLine(sets[item.Key] + ": ");
                foreach (var card in item.Value)
                {
                    var colors = string.Empty;
                    foreach (var color in card.Colors)
                    {
                        if (colors.Equals(string.Empty))
                        {
                            colors += color;
                        }
                        else
                        {
                            colors += ", " + color;
                        }                        
                    }

                    if (colors.Equals(string.Empty))
                    {
                        colors = card.Type;
                    }

                    Console.WriteLine("\t" + $"{card.Name} <> {colors}");
                }
            }
        }

        static List<KeyValuePair<string, int>> GetSetCounts(Dictionary<string, List<Card>> cards, Dictionary<string, string> sets)
        {
            var counts = new Dictionary<string, int>();

            foreach (var cardVersions in cards)
            {
                foreach (var card in cards[cardVersions.Key])
                {
                    if (counts.ContainsKey(card.Set))
                    {
                        counts[card.Set] += 1;
                    }
                    else
                    {
                        counts[card.Set] = 1;
                    }
                }
            }

            return counts.OrderByDescending(x => x.Value).ToList();
        }
    }
}
