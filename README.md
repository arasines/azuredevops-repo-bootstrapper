# Azure DevOps Repositoy Bootstrapper
This program aims to achieve a seamless synchronization process between an Azure DevOps Repository and a local repository. 
By utilizing smart synchronization techniques, it ensures that the most up-to-date code and assets are always mirrored between the two environments.

These types of repo synchronization coudl be extremely usefull when a reference framework for cross-cutting concers to accelerate the development process of new apps and/or APIs, guaranteeing a the same time architecture and tools/libraries alignment.

## Key Features
<ul>
<li><i>Automatic Detection</i>: The program intelligently detects changes in the Azure DevOps Repository and only synchronizes modified or new files, reducing unnecessary data transfer.</li>
<li><i>Efficient Data Transfer</i>: Through optimized algorithms, the program minimizes data transfer times while ensuring the integrity of the transferred files.</li>
<li><i>Customizable Sync Settings</i>: Users have the flexibility to define synchronization intervals, specific branches, and exclusion rules to tailor the synchronization process to their project's needs.</li>
<li><i>Error Handling</i>: The program incorporates robust error-handling mechanisms to manage unexpected scenarios and provide clear error messages to users.</li>
</ul>

## Getting Started
<ol>
<li>Open <code>Bootstrap.cmd</code> and replace the string <AZURE_DEVOPS_REPO_URL> with the you URL</li>
<li>Open <code>Bootstrap.csx</code> to configure define the update , ignored and add-only folders. Ad-Hoc replacements are also available.</li>
<li>Execute the CMD file.</li>
<li>Check for the results.</li>
<li>Start/continue you own coding and happy coding!</li>
</ol>