using Amazon.Route53Domains;
using Amazon.Route53Domains.Model;

namespace aws_service.Constants
{
    public static class DomainConstants
    {
        // TODO: Please review this information and update accordingly
        public static RegisterDomainRequest RegisterDomainRequest => new RegisterDomainRequest
        {
            AdminContact = ContactConstant.Contact,
            RegistrantContact = ContactConstant.Contact,
            TechContact = ContactConstant.Contact,
            AutoRenew = true,
            DurationInYears = 1,
            PrivacyProtectAdminContact = true,
            PrivacyProtectRegistrantContact = true,
            PrivacyProtectTechContact = true
        };
    }

    public static class ContactConstant
    {
        // TODO: Please update this information
        public static ContactDetail Contact => new ContactDetail
        {
            AddressLine1 = "Carrera 41 No. .57 Sur 01",
            City = "Sabaneta",
            ContactType = ContactType.PERSON,
            CountryCode = "CO",
            Email = "ing.willym@gmail.com",
            FirstName = "Willy",
            LastName = "Mendoza",
            PhoneNumber = "+573.143034290",
            ZipCode = "055450"
        };
    }
}
