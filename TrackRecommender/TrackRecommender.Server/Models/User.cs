namespace TrackRecommender.Server.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserPreferences Preferences { get; set; }

        public User(int id, string username, string email, string password, UserPreferences preferences)
        {
            Id = id;
            Username = username;
            Email = email;
            Password = password;
            Preferences = preferences;
        }
    }
}