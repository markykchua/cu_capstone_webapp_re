# cu_capstone_webapp_re
Reverse Engineering a Web application. 

Let us create a math format for the problem we want to resolve. Denote the web application under reverse engineering as a function $f(x)$ in which an instance of $x$ is an API. Denote the complete set of APIs for a particular $f$ as $\Alpha$. We may know a subset of $\Alpha$. The task is to implement an automated method to  find the complete $\Alpha$ or at least increase the size of the known API set. 

## Related tools/projects
MITMProxy2Swagger: https://github.com/alufers/mitmproxy2swagger 
POSTMAN: https://www.postman.com
Zaproxy: https://github.com/zaproxy/zaproxy
Restler-fuzzer: https://github.com/microsoft/restler-fuzzer?tab=readme-ov-file

## Tentative features to be delivered by the project
- discover undocumented APIs
- discover the business logic via the APIs discovered.
- orchestrate the API calls to automate a user journey


