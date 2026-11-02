using Chronicle.Prototyping;

Simulation sim = Simulation.NewGame();
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("Летопись — консольный эксперимент: слухи и письма. help — команды.");
while (true)
{
    Console.Write("> ");
    string? line = Console.ReadLine();
    if (line is null) break;
    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) continue;
    try
    {
        switch (parts[0].ToLowerInvariant())
        {
            case "help":
                Console.WriteLine("status | people | inspect ID | send ID true/false | wait N | journal | save FILE | load FILE | quit");
                break;
            case "status":
                Console.WriteLine(sim.Status()); break;
            case "people":
                foreach (var p in sim.State.People)
                    Console.WriteLine($"{p.Id}: {p.Name}, влияние {p.Influence}");
                break;
            case "inspect" when parts.Length == 2 && int.TryParse(parts[1], out int id):
                var person = sim.GetPerson(id);
                Console.WriteLine($"{person.Name}; связи с вами: доверие {person.Trust.GetValueOrDefault(sim.State.PlayerId)}");
                if (id == sim.State.PlayerId)
                    Console.WriteLine(sim.Status());
                else Console.WriteLine("Чужие знания скрыты.");
                break;
            case "send" when parts.Length == 3 && int.TryParse(parts[1], out int recipient) && bool.TryParse(parts[2], out bool claim):
                Console.WriteLine(sim.SendRumor(sim.State.PlayerId, recipient, 1, claim).Text);
                break;
            case "wait" when parts.Length == 2 && int.TryParse(parts[1], out int days):
                sim.Tick(days); Console.WriteLine(sim.Status()); break;
            case "journal":
                foreach (var evt in sim.State.History.TakeLast(15)) Console.WriteLine(evt);
                break;
            case "save" when parts.Length == 2:
                sim.Save(parts[1]); Console.WriteLine("Сохранено."); break;
            case "load" when parts.Length == 2:
                sim = Simulation.Load(parts[1]); Console.WriteLine("Загружено."); break;
            case "quit":
            case "exit":
                return;
            default:
                Console.WriteLine("Неизвестная команда. help — список.");
                break;
        }
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
    {
        Console.WriteLine("Ошибка: " + ex.Message);
    }
}
