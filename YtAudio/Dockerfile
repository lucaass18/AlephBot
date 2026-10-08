# o yt-audio: o yt-dlp com o plugin do gerador de token e o deno, servindo o áudio por HTTP
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

COPY requirements.txt .
RUN pip install -r requirements.txt

COPY *.py ./

# os testes rodam aqui, com o yt-dlp e o YouTube de mentira: se quebrar, a imagem nem sai
RUN python -m unittest -q test_regras test_servidor

# usuário sem privilégio, com casa própria: o yt-dlp e o deno guardam cache em ~/.cache
RUN useradd --create-home --uid 10001 audio
USER audio

EXPOSE 8080
CMD ["python", "servidor.py"]
