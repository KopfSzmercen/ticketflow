---
name: azure-bicep
description: Guide for selecting Azure Resource, implementing Azure Resource using Azure Bicep.
---

To implement Azure Resource using Azure Bicep, you can follow these steps using tools provided by Azure MCP:

1. Present developer with core decisions to make when choosing resource such as SKU, kind or plan.
2. Consider with developer security requirements, accesses and artchitecture of the resource to be implemented.
3. Focus on creating a Bicep file that defines the Azure Resource with the selected options
4. Provide guidance on how to deploy the Bicep file to Azure
5. Create architecture decision record (ADR) in /adr folder, to document the decisions made during the implementation process, including the rationale behind those decisions and any trade-offs considered. This will help ensure that future developers understand the context and reasoning behind the choices made.
6. The project should use managed identities to reduce the need for secrets and credentials in the codebase. Ensure proper configuration of managed identities for the Azure resources being implemented, and document the setup process in the ADR.
7. After all steps run:
   az bicep build --file infra/main.bicep
   to validate syntax and  
   az deployment sub what-if \
    --location polandcentral \
    --template-file infra/main.bicep \
    --parameters infra/parameters/dev.bicepparam
   to confirm that the changes we'll get match our expectations

To get latest Bicep templates and examples you can refer to the official Microsift Azure documentation:
https://learn.microsoft.com/en-us/azure/templates
