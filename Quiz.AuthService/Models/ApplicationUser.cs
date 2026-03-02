using AspNetCore.Identity.Mongo.Model;
using MongoDbGenericRepository.Attributes;

namespace Quiz.AuthService.Models;

[CollectionName("Users")]
public class ApplicationUser : MongoUser<Guid>
{
}