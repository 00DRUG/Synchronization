# Synchronization


This project is a C# implementation of a synchronization system that includes logging, synchronization mechanisms, utility functions, and models for handling input parameters and log messages.

## Project Structure

### 📂 Enums
Contains enumeration types used throughout the project.

- **ComparisonMethod.cs** – Defines different methods for comparing values.
- **LogLevel.cs** – Specifies various logging levels (e.g., Info, Warning, Error).
- **OperationType.cs** – Represents different types of operations performed by the system.

### 📂 Extensions
Provides extension methods to enhance functionality.

- **ILoggerExtensions.cs** – Contains extension methods for the `ILogger` interface to streamline logging operations.
- **InputParametersExtensions.cs** – Provides extension methods for handling input parameters efficiently.

### 📂 Interfaces
Defines contracts for logging and synchronization.

- **ILogger.cs** – Interface for logging functionalities, ensuring a consistent logging mechanism.
- **ISynchronizer.cs** – Interface defining the contract for synchronization implementations.

### 📂 Models
Contains data models used within the project.

- **FilesContex.cs** – Represents the context for file operations.
- **InputParameters.cs** – Model for handling input parameters required in synchronization.
- **LogMessage.cs** – Structure for log messages, encapsulating relevant log details.

### 📂 Services
Contains core service implementations.

- **Logger.cs** – Implements the `ILogger` interface, handling logging operations.
- **Starter.cs** – Manages the initialization and execution of synchronization.
- **Synchronizer.cs** – Implements the `ISynchronizer` interface, handling synchronization logic.

### 📂 Utils
Utility classes for various helper functions.

- **Comparators.cs** – Provides utility methods for comparing different objects or values.
- **CtsUtils.cs** – Contains helper methods related to `CancellationTokenSource` and threading operations.
- **ParserUtils.cs** – Utility functions for parsing input data.

### 📄 Program.cs (Main Entry Point)
The `Program.cs` file initializes the application, processes command-line arguments, and starts the synchronization workflow.

- Uses `CtsUtils` to handle cancellation tokens.
- Parses console arguments using `ParserUtils`.
- Initializes the `Logger`, `FileSynchronizer`, and `Starter` services.
- Runs the synchronization process asynchronously.

🚧 **This is a preliminary version of the README file, intended for observation and review. A more detailed version will be provided later.** 🚧
