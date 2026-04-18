using Donnees;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Metier.CRM
{
    public class AnnuaireService
    {
        private readonly ErpDbContext _context;

        public AnnuaireService(ErpDbContext context)
        {
            _context = context;
        }

        public async Task<List<Contact>> GetAllContactsAsync()
        {
            return await _context.Contacts.OrderByDescending(c => c.DateCreation).ToListAsync();
        }

        public async Task<Contact?> GetContactByIdAsync(int id)
        {
            return await _context.Contacts.FindAsync(id);
        }

        public async Task<Contact> SaveContactAsync(Contact contact)
        {
            if (contact.Id == 0)
            {
                _context.Contacts.Add(contact);
            }
            else
            {
                _context.Contacts.Update(contact);
            }
            await _context.SaveChangesAsync();
            return contact;
        }
        
        public async Task DeleteContactAsync(int id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<ContactRelation>> GetAllRelationsAsync()
        {
            return await _context.ContactRelations.ToListAsync();
        }

        public async Task<List<ContactRelation>> GetRelationsForContactAsync(int contactId)
        {
            return await _context.ContactRelations
                .Include(r => r.SourceContact)
                .Include(r => r.TargetContact)
                .Where(r => r.SourceContactId == contactId || r.TargetContactId == contactId)
                .ToListAsync();
        }

        public async Task<ContactRelation> AddRelationAsync(int sourceId, int targetId, string relationType)
        {
            var relation = new ContactRelation
            {
                SourceContactId = sourceId,
                TargetContactId = targetId,
                RelationType = relationType
            };
            _context.ContactRelations.Add(relation);
            await _context.SaveChangesAsync();
            return relation;
        }

        public async Task DeleteRelationAsync(int id)
        {
            var rel = await _context.ContactRelations.FindAsync(id);
            if (rel != null)
            {
                _context.ContactRelations.Remove(rel);
                await _context.SaveChangesAsync();
            }
        }
    }
}
