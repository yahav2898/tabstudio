namespace ContactBook.Models
{
    /// <summary>The contacts the app starts with.</summary>
    public static class SampleData
    {
        public static IEnumerable<Contact> CreateContacts() =>
        [
            new Contact { Name = "Ada Lovelace", Company = "Analytical Engines", Email = "ada@engines.example", Phone = "555-0101", IsFavorite = true, Rating = 5 },
            new Contact { Name = "Grace Hopper", Company = "Navy Computing", Email = "grace@navy.example", Phone = "555-0102", Rating = 4 },
            new Contact { Name = "Alan Turing", Company = "Bletchley Park", Email = "alan@bletchley.example", Phone = "555-0103", IsFavorite = true, Rating = 5 },
            new Contact { Name = "Margaret Hamilton", Company = "Apollo Guidance", Email = "margaret@apollo.example", Phone = "555-0104", Rating = 3 },
        ];
    }
}
