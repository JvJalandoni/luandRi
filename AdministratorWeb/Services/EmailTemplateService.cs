using System.Collections.Generic;
using System.Text;

namespace AdministratorWeb.Services
{
    public interface IEmailTemplateService
    {
        string RenderTemplate(string template, Dictionary<string, string> variables);
    }

    public class EmailTemplateService : IEmailTemplateService
    {
        public string RenderTemplate(string template, Dictionary<string, string> variables)
        {
            var result = template;
            foreach (var variable in variables)
            {
                result = result.Replace($"{{{{{variable.Key}}}}}", variable.Value);
            }
            return result;
        }
    }
}
