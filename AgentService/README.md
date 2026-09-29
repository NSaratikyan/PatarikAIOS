# Patarik AI OS — Business Manager Agent

This folder hosts the OpenAI Agents SDK service used by the WPF desktop application.

## Local setup

1. Install Python 3.11+.
2. Create a virtual environment.
3. Install dependencies:

   `pip install -r requirements.txt`

4. Set your OpenAI API key in the environment:

   PowerShell: `$env:OPENAI_API_KEY="..."`

5. Start the service:

   `uvicorn main:app --host 127.0.0.1 --port 8765`

6. Verify `http://127.0.0.1:8765/health` returns `{ "status": "ok" }`.

## Safety model

The language model is not the source of truth for financial calculations. Patarik AI OS continues to calculate business figures and deterministic recommendations in C#. The agent receives those verified results as request context and may explain, prioritize, and combine them. It must not invent figures or claim external actions were executed.

The first version is intentionally read-only. Future write tools (payments, supplier orders, inventory changes) should require explicit approval in the desktop application before execution.
