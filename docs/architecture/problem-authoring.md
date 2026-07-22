# Problem authoring backend

The application layer exposes authorized draft creation, MAS validation, bounded preview, generation status, cancellation, and atomic-store publication abstractions. Preview never publishes tests and returns seed, runtime version, diagnostics, bounded inputs, and truncation state. Generation reports failures with stable codes and asks the store to publish the complete set once.

The current repository does not yet provide the PostgreSQL `IProblemStudioStore`, object ingestion, HTTP Problem Studio endpoints, reference-output worker jobs, or complete import/export persistence. Reference solutions must eventually use durable authenticated judge workers; the API server and MAS runtime must never execute native code.
