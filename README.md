# Ac682.Hyperai.Clients.CQHTTP

实际对标的是 go-cqhttp 而不是 cqhttp.
~~可惜的是这两者都没有api文档可以参考(前者说看后者,后者倒了,onebot接口和实际的go-http返回值不一致),现在就是摸着石头过河,全靠一个一个试验出来.~~
[旧文档](https://richardchien.gitee.io/coolq-http-api/docs/4.15)找到了

## 部署

同隔壁 [Mirai Adapter](https://github.com/ac682/Ac682.Hyperai.Clients.Mirai)，`Options` 只需要填 `Host`, `Port`, `AccessToken` 就行。

## 实现与未实现

- [ ] 大部分
- [ ] MessageId 获取, 因为是异步的, 所以没法在函数返回前提供 MessageId
- [x] 小部分
- [ ] GroupMessageEventArgs.Group仅包含Indentity,因为api只给了group_id..

### 发送事件

- [x] 私聊消息
- [x] 群聊消息
- [ ] 其他所有

## 消息元素

- [x] Plain
- [x] Image
- [x] Face
- [ ] Flash
- [x] At
- [x] AtAll
- [x] Quote
- [ ] Source, 没有MessageId, 也意味着无法回复了
- [ ] 所有 ContentBase 派生
- [x] 有什么不懂就塞给 Unknown