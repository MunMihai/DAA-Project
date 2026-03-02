using AspNetCore.Identity.Mongo.Model;
using MongoDbGenericRepository.Attributes;

namespace Quiz.AuthService.Models;

[CollectionName("Roles")]
public class ApplicationRole : MongoRole<Guid>
{
}