FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

COPY --link Quiz.Web/app/nginx.conf /etc/nginx/conf.d/default.conf
COPY --link .artifacts/web/ /usr/share/nginx/html

EXPOSE 8080
