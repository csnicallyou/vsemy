using System.Text.Json;

namespace Chronicle.Prototyping;

// No Godot or console dependency. The same API can later be called from any UI.
public sealed class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Influence { get; set; }
    public int Money { get; set; }
    public Dictionary<int, int> Trust { get; set; } = new();
    public List<Belief> Beliefs { get; set; } = new();
}

public sealed class Belief
{
    public int FactId { get; set; }
    public bool ClaimedValue { get; set; }
    public int Confidence { get; set; }
    public int SourceId { get; set; }
    public int LearnedDay { get; set; }
}

public sealed class Fact
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public bool Truth { get; set; }
}

public sealed class Message
{
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
    public int FactId { get; set; }
    public bool ClaimedValue { get; set; }
    public int ArrivalDay { get; set; }
}

public sealed class World
{
    public int Day { get; set; }
    public int PlayerId { get; set; } = 1;
    public List<Person> People { get; set; } = new();
    public List<Fact> Facts { get; set; } = new();
    public List<Message> Messages { get; set; } = new();
    public List<string> History { get; set; } = new();
}

public sealed record Result(bool Success, string Text);

public sealed class Simulation(World world)
{
    public World State { get; } = world;
    public Person Player => GetPerson(State.PlayerId);

    public static Simulation NewGame()
    {
        var w = new World();
        w.People.Add(new Person { Id = 1, Name = "Мартин Рудный", Influence = 2, Money = 30, Trust = new() { [2] = 20, [3] = -10 } });
        w.People.Add(new Person { Id = 2, Name = "Граф Альбрехт", Influence = 7, Money = 100, Trust = new() { [1] = 25, [3] = -30 } });
        w.People.Add(new Person { Id = 3, Name = "Казначей Оскар", Influence = 5, Money = 85, Trust = new() { [1] = 0, [2] = 10 } });
        w.Facts.Add(new Fact { Id = 1, Text = "Казначей скрывает часть доходов от пошлин", Truth = true });
        // Player has a rumor, not direct access to the authoritative fact.
        w.People[0].Beliefs.Add(new Belief { FactId = 1, ClaimedValue = true, Confidence = 40, SourceId = 3, LearnedDay = 0 });
        w.History.Add("[0] В городе распространяются слухи о недостаче в казне.");
        return new Simulation(w);
    }

    public Person GetPerson(int id) => State.People.Single(p => p.Id == id);
    public Fact GetFact(int id) => State.Facts.Single(f => f.Id == id);

    public IReadOnlyList<Belief> Known(int personId) => GetPerson(personId).Beliefs.AsReadOnly();

    public Result SendRumor(int actorId, int recipientId, int factId, bool claim)
    {
        if (actorId != State.PlayerId) return new(false, "Нет права отправлять приказ от чужого лица.");
        if (actorId == recipientId || !State.People.Any(p => p.Id == recipientId)) return new(false, "Недоступный адресат.");
        var actor = GetPerson(actorId);
        if (!actor.Beliefs.Any(b => b.FactId == factId)) return new(false, "Персонаж не знает даже о существовании этой темы.");
        if (State.Messages.Any(m => m.SenderId == actorId && m.RecipientId == recipientId && m.FactId == factId))
            return new(false, "По этой теме письмо уже в пути.");
        if (actor.Money < 2) return new(false, "Не хватает 2 серебра на гонца.");
        actor.Money -= 2;
        State.Messages.Add(new Message { SenderId = actorId, RecipientId = recipientId, FactId = factId, ClaimedValue = claim, ArrivalDay = State.Day + 3 });
        State.History.Add($"[{State.Day}] {actor.Name} отправил гонца к {GetPerson(recipientId).Name}.");
        return new(true, "Письмо отправлено. Прибытие через 3 дня; содержание не становится общим знанием.");
    }

    public void Tick(int days = 1)
    {
        if (days is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(days));
        for (int i = 0; i < days; i++)
        {
            State.Day++;
            var arrived = State.Messages.Where(m => m.ArrivalDay <= State.Day).ToList();
            foreach (var m in arrived)
            {
                var receiver = GetPerson(m.RecipientId);
                var sender = GetPerson(m.SenderId);
                int trust = receiver.Trust.GetValueOrDefault(sender.Id);
                int confidence = Math.Clamp(30 + trust / 2, 5, 80);
                var prior = receiver.Beliefs.FirstOrDefault(b => b.FactId == m.FactId);
                if (prior is null)
                    receiver.Beliefs.Add(new Belief { FactId = m.FactId, ClaimedValue = m.ClaimedValue, Confidence = confidence, SourceId = sender.Id, LearnedDay = State.Day });
                else if (prior.ClaimedValue == m.ClaimedValue)
                {
                    prior.Confidence = Math.Min(95, prior.Confidence + Math.Max(5, confidence / 4));
                    prior.LearnedDay = State.Day;
                }
                else if (confidence > prior.Confidence)
                {
                    prior.ClaimedValue = m.ClaimedValue;
                    prior.Confidence = confidence;
                    prior.SourceId = sender.Id;
                    prior.LearnedDay = State.Day;
                }
                else
                    prior.Confidence = Math.Max(5, prior.Confidence - 10);
                State.History.Add($"[{State.Day}] {receiver.Name} получил письмо от {sender.Name} и оценил услышанное по-своему.");
                State.Messages.Remove(m);
            }
        }
    }

    public string Status()
    {
        var me = Player;
        var beliefs = me.Beliefs.Select(b => $"Тема {b.FactId}: «{GetFact(b.FactId).Text}» — {(b.ClaimedValue ? "вероятно да" : "вероятно нет")}, уверенность {b.Confidence}%");
        return $"День {State.Day} | {me.Name} | серебро {me.Money}\n" +
               (beliefs.Any() ? string.Join("\n", beliefs) : "Известных слухов нет.") +
               $"\nПисем в пути: {State.Messages.Count}";
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
    public static Simulation Load(string path)
    {
        var world = JsonSerializer.Deserialize<World>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Пустое сохранение");
        if (world.People.Count == 0 || world.People.All(p => p.Id != world.PlayerId))
            throw new InvalidDataException("Некорректный состав мира");
        return new Simulation(world);
    }
}
