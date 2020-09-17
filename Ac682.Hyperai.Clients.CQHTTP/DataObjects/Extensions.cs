using Hyperai.Relations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP.DataObjects
{
    public static class Extensions
    {
        public static Member ToMember(this DtoGroupSender target, Group group)
        {
            Member member = new Member()
            {
                DisplayName = string.IsNullOrEmpty(target.Card) ? target.Nickname : target.Card,
                Group = new Lazy<Group>(group),
                Identity = target.UserId,
                Title = target.Title,
                Nickname = target.Nickname,
                // TODO: more roles
                Role = target.Role switch
                {
                    "owner" => GroupRole.Owner,
                    _ => GroupRole.Member
                },
            };
            return member;
        }

        public static Friend ToFriend(this DtoFriendSender target)
        {
            Friend friend = new Friend()
            {
                Nickname = target.Nickname,
                Identity = target.UserId,
                Remark = target.Nickname,
            };
            return friend;
        }
    }
}
