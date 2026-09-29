import os
from typing import Any

from agents import Agent, Runner, function_tool
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI(title="Patarik AI Business Manager", version="0.1.0")


class AgentRequest(BaseModel):
    message: str = Field(min_length=1)
    business_context: dict[str, Any] = Field(default_factory=dict)


class AgentResponse(BaseModel):
    answer: str


_RUNTIME_CONTEXT: dict[str, Any] = {}


@function_tool
def get_business_snapshot() -> dict[str, Any]:
    """Return the verified business snapshot supplied by Patarik AI OS for this request."""
    return _RUNTIME_CONTEXT.get("snapshot", {})


@function_tool
def get_business_rules() -> dict[str, Any]:
    """Return deterministic recommendations and guardrails calculated by Patarik AI OS."""
    return {
        "recommendations": _RUNTIME_CONTEXT.get("recommendations", []),
        "rules": [
            "Never invent sales, debt, stock, payment or cash figures.",
            "Use only values returned by tools or explicitly provided by the user.",
            "Treat deterministic Patarik AI OS calculations as authoritative for numeric business decisions.",
            "Do not claim that a payment, purchase, supplier order or other external action was executed unless the application confirms it.",
            "When an action affects money, inventory or supplier commitments, present it as a recommendation unless the application explicitly exposes an approved execution tool."
        ],
    }


manager = Agent(
    name="Patarik Business Manager",
    instructions=(
        "You are the Business Manager inside Patarik AI OS, a bakery and retail management application. "
        "Answer in the same language as the user unless they ask otherwise. "
        "Your job is to explain verified business data, prioritize issues, and recommend concrete next actions. "
        "Always call get_business_snapshot when the question depends on current business figures. "
        "Call get_business_rules when giving recommendations. "
        "Never manufacture numbers or pretend an external action has happened. "
        "Keep answers concise, operational and suitable for a business owner."
    ),
    tools=[get_business_snapshot, get_business_rules],
)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/agent/ask", response_model=AgentResponse)
async def ask_agent(request: AgentRequest) -> AgentResponse:
    if not os.getenv("OPENAI_API_KEY"):
        raise HTTPException(status_code=503, detail="OPENAI_API_KEY is not configured")

    global _RUNTIME_CONTEXT
    _RUNTIME_CONTEXT = {
        "snapshot": request.business_context.get("snapshot", {}),
        "recommendations": request.business_context.get("recommendations", []),
    }
    try:
        result = await Runner.run(manager, request.message)
        return AgentResponse(answer=str(result.final_output))
    finally:
        _RUNTIME_CONTEXT = {}
