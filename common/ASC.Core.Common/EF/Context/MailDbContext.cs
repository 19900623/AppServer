﻿using ASC.Core.Common.EF.Model.Mail;

using Microsoft.EntityFrameworkCore;

namespace ASC.Core.Common.EF.Context
{
    public class MailDbContext : BaseDbContext
    {
        public DbSet<MailboxServer> MailboxServer { get; set; }
        public DbSet<ServerServer> ServerServer { get; set; }
        public DbSet<MailboxProvider> MailboxProvider { get; set; }
        public DbSet<Mailbox> Mailbox { get; set; }
        public DbSet<ApiKeys> ApiKeys { get; set; }
        public DbSet<GreyListingWhiteList> GreyListingWhiteList { get; set; }

        public MailDbContext() { }
        public MailDbContext(DbContextOptions<MailDbContext> options)
            : base(options)
        {
        }
    }
}
